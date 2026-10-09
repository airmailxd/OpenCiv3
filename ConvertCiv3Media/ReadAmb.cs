using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace ConvertCiv3Media;

public struct Sfx {
	public float delayStart; // seconds before start playing
							 // public float duration; // do we even need this?
	public string wavName;
	public bool speedRandom;
	public int minRandomSpeed; // per cent increase/decrease
	public int maxRandomSpeed; // per cent increase/decrease
	public bool volumeRandom;
	public int minRandomVolume; // per cent of original 100%
	public int maxRandomVolume; // per cent of original 100%
}

public class Amb {
	private static ILogger log = Log.ForContext<Amb>();
	private AmbData ambData;
	private string path;

	public List<Sfx> soundEffects { get; set; } = new List<Sfx>();

	// The MIDI default tempo, 120 beats per minute, for files without a tempo
	private const int DEFAULT_MICROSECONDS_PER_QUARTER_NOTE = 500_000;

	public Amb(string path) {
		this.path = path;
		this.ambData = new AmbData(path);
		Process();
	}

	// Each sound track plays one sound: its program change event selects a prgm chunk by its program number, which has
	// the playback settings, and the prgm chunk's variable name selects the kmap chunk with the wav file
	private void Process() {
		MidiData midi = ambData.midiData;
		if (midi == null) {
			throw new InvalidDataException($"No MIDI data found in {this.path}");
		}
		SoundTrack infoTrack = midi.soundTracks.FirstOrDefault(t => t != null && t.IsInfoTrack());
		if (infoTrack == null) {
			throw new InvalidDataException($"No info track found in {this.path}");
		}

		int microsecondsPerQuarterNote = infoTrack.setTempoEvent?.microsecondsPerQuarterNote ?? DEFAULT_MICROSECONDS_PER_QUARTER_NOTE;
		var secondsPerQuarterNote = microsecondsPerQuarterNote / 1_000_000.0f;
		var secondsPerTick = 0.0f;

		if (midi.ticksPerQuarterNote > 0) {
			secondsPerTick = secondsPerQuarterNote / midi.ticksPerQuarterNote;
		} else if (midi.ticksPerQuarterNote < 0) {
			// SMPTE time (no Civ3 AMB uses it): the high byte is minus the frames per second, the low byte the ticks
			// per frame, and the tempo doesn't matter.
			int framesPerSecond = -(sbyte)(midi.ticksPerQuarterNote >> 8);
			int ticksPerFrame = midi.ticksPerQuarterNote & 0xff;
			if (framesPerSecond > 0 && ticksPerFrame > 0) {
				secondsPerTick = 1f / (framesPerSecond * ticksPerFrame);
			} else {
				log.Warning("Invalid SMPTE MIDI division in {path}; playing all sounds at once", this.path);
			}
		} else {
			log.Warning("MIDI division of 0 in {path}; playing all sounds at once", this.path);
		}

		foreach (SoundTrack st in midi.soundTracks) {
			// skip the info track, and tracks that don't play anything
			if (st == null || st == infoTrack || st.NoteOnEvent == null) {
				continue;
			}
			var delay = st.NoteOnEvent.timeDelta * secondsPerTick;

			PrgmChunk prgmChunk = FindPrgmChunk(st);
			if (prgmChunk == null) {
				log.Warning("No prgm chunk for track {track} (program {program}) in {path}", st.TrackNameEvent?.trackName, st.programChangeEvent?.programNumber, this.path);
				continue;
			}
			KmapChunk kmapChunk = FindKmapChunk(prgmChunk);
			string wavFileName = PickKmapItem(kmapChunk, st.NoteOnEvent.key)?.wavFileName;

			if (string.IsNullOrEmpty(wavFileName)) {
				// Attempt to correct some known broken .amb
				// TODO: move to lua maybe?
				bool isArcherRun = Path.GetFileName(this.path).Equals("ArcherRun.amb", StringComparison.OrdinalIgnoreCase);
				if (isArcherRun && prgmChunk.varName == "Breath 1") {
					wavFileName = "ArchRunBreath1.wav";
				} else if (isArcherRun && prgmChunk.varName == "Breath 2") {
					wavFileName = "ArchRunBreath2.wav";
				} else {
					log.Warning("Missing wav file name for {name} in {path}", prgmChunk.varName, this.path);
					continue;
				}
			}

			var entry = new Sfx() {
				delayStart = delay,
				wavName = wavFileName,
				speedRandom = prgmChunk.randomizePlaybackSpeed,
				minRandomSpeed = NormalizeSpeed(prgmChunk.minRandomSpeed),
				maxRandomSpeed = NormalizeSpeed(prgmChunk.maxRandomSpeed),
				volumeRandom = prgmChunk.randomizeVolume,
				minRandomVolume = NormalizeVolume(prgmChunk.minRandomVolume),
				maxRandomVolume = NormalizeVolume(prgmChunk.maxRandomVolume),
			};

			soundEffects.Add(entry);
		}

		soundEffects = soundEffects.OrderBy(e => e.delayStart).ToList();
	}

	// The prgm chunk with the track's program number. A few files have a track whose program number has no prgm chunk
	// (e.g. ChariotAttack.amb, where two prgm chunks have the same number); then the prgm chunk named like the track is used.
	private PrgmChunk FindPrgmChunk(SoundTrack track) {
		if (track.programChangeEvent != null) {
			PrgmChunk byNumber = ambData.prgmChunks.FirstOrDefault(p => p.index == track.programChangeEvent.programNumber);
			if (byNumber != null) {
				return byNumber;
			}
		}
		string trackName = track.TrackNameEvent?.trackName;
		if (string.IsNullOrEmpty(trackName)) {
			return null;
		}
		return ambData.prgmChunks.FirstOrDefault(p => string.Equals(p.effectName, trackName, StringComparison.OrdinalIgnoreCase))
			?? ambData.prgmChunks.FirstOrDefault(p => string.Equals(p.varName, trackName, StringComparison.OrdinalIgnoreCase));
	}

	// The kmap item to play for a note. Every kmap item in Civ3's AMB files has unknown1 = 127, unknown2 = 0 and
	// unknown3 = 1, which looks like a MIDI key range (high, low) as in a sampler key map, and no Civ3 AMB has more
	// than one item per kmap (1532 kmaps with one item and 14 with none, in the Complete edition). So with several
	// items, the first whose range holds the note's key is played, but that reading of the fields is unverified;
	// failing it, the first item is.
	private KmapItem PickKmapItem(KmapChunk kmap, int key) {
		if (kmap == null || kmap.items.Length == 0) {
			return null;
		}
		if (kmap.items.Length == 1) {
			return kmap.items[0];
		}
		foreach (KmapItem item in kmap.items) {
			if (key >= Math.Min(item.unknown1, item.unknown2) && key <= Math.Max(item.unknown1, item.unknown2)) {
				return item;
			}
		}
		log.Debug("No kmap item of {name} covers key {key} in {path}; using the first", kmap.varName, key, this.path);
		return kmap.items[0];
	}

	// The kmap chunk with the prgm chunk's variable name (ignoring case, which sometimes differs). Failing that (the
	// variable name in GalleyAttack.amb's kmap chunk is garbled), the kmap chunk in the same position as the prgm chunk.
	private KmapChunk FindKmapChunk(PrgmChunk prgmChunk) {
		KmapChunk byName = ambData.kmapChunks.FirstOrDefault(k => string.Equals(k.varName, prgmChunk.varName, StringComparison.OrdinalIgnoreCase));
		if (byName != null) {
			return byName;
		}
		int position = ambData.prgmChunks.IndexOf(prgmChunk);
		return position < ambData.kmapChunks.Count ? ambData.kmapChunks[position] : null;
	}

	// the amb data being midi, have an upper bound of 127,
	// so we transform it to an even 100 so that we can work with it more easily
	private int NormalizeVolume(int volume) {
		float result = volume * (100f / 127);
		return (int)result;
	}
	// 100 speed corresponds to roughly 6% increase/decrease in speed, that's where the 6 is coming from.
	// So an item with speed 200, will play ~12% faster, and with speed -200 will play ~12% slower
	private int NormalizeSpeed(int speed) {
		float result = 6f * (speed / 100f);
		return (int)result;
	}
}

/*
    Some useful links on parsing .amb & .mid(i) files

    https://www.recordingblogs.com/wiki/midi-meta-messages
    https://www.mixagesoftware.com/en/midikit/help/HTML/midi_events.html
    https://www.mixagesoftware.com/en/midikit/help/HTML/meta_events.html
    https://www.youtube.com/watch?v=P27ml4M3V7A
    https://forums.civfanatics.com/threads/amb-sound-editor.698491/
    https://github.com/maxpetul/Civ3AMBAnalysis/blob/master/AMBFormat.org
    https://github.com/maxpetul/C3X/blob/master/AMB%20Editor/amb_file.c
    https://www.skytopia.com/project/articles/midi.html
 */
// Throws InvalidDataException for malformed files.
public class AmbData {
	private static ILogger log = Log.ForContext<AmbData>();

	public List<PrgmChunk> prgmChunks = new List<PrgmChunk>();
	public List<KmapChunk> kmapChunks = new List<KmapChunk>();
	public GlblChunk glblChunk;
	public MidiData midiData;

	public AmbData(string path) {
		this.Load(path);
	}

	private struct VarLenItem(int len, int val) {
		public int length = len;
		public int value = val;
	}

	private const int HEADER_SIZE = 8; // 4 for header tag + 4 for the size field itself
	private const int PRGM_FIXED_SIZE = 28; // the values before the names
	private const int MTHD_SIZE = 14;

	private static readonly System.Text.ASCIIEncoding ascii = new System.Text.ASCIIEncoding();

	private void Load(string path) {
		if (!path.EndsWith(".amb", StringComparison.CurrentCultureIgnoreCase)) {
			throw new ApplicationException($"Invalid file type for file: `{path}`. Only .amb files are supported.");
		}
		log.Debug("Parsing {path}", path);

		byte[] ambBytes = File.ReadAllBytes(path);

		int offset = 0;
		List<SoundTrack> soundTracks = new List<SoundTrack>();

		while (offset < ambBytes.Length) {
			if (ambBytes.Length - offset < HEADER_SIZE) {
				log.Debug("Ignoring {count} bytes at the end of {path}", ambBytes.Length - offset, path);
				break;
			}
			int header = BitConverter.ToInt32(ambBytes, offset);

			switch (header) {
				case 0x6d677270: { // prgm
						int size = ChunkSize(ambBytes, offset, PRGM_FIXED_SIZE, "prgm"); // does not count itself or the header tag
						int chunkEnd = offset + HEADER_SIZE + size;
						var prgm = new PrgmChunk() {
							size = size,
							index = BitConverter.ToInt32(ambBytes, offset + 8),
							randomizePlaybackSpeed = GetFlag(ambBytes[offset + 12], 0),
							randomizeVolume = GetFlag(ambBytes[offset + 12], 1),
							maxRandomSpeed = BitConverter.ToInt32(ambBytes, offset + 16),
							minRandomSpeed = BitConverter.ToInt32(ambBytes, offset + 20),
							maxRandomVolume = BitConverter.ToInt32(ambBytes, offset + 24),
							minRandomVolume = BitConverter.ToInt32(ambBytes, offset + 28),
						};
						// skip 4 bytes for 0xFA that terminates the chunk (early)
						var eff = GetNullTerminatedString(ambBytes, offset + 36, chunkEnd);
						prgm.effectName = eff.text;
						var var = GetNullTerminatedString(ambBytes, offset + 36 + eff.size + 1, chunkEnd); // +1 to account for the terminating byte 0x00
						prgm.varName = var.text;
						this.prgmChunks.Add(prgm);
						offset = chunkEnd;
						break;
					}
				case 0x70616d6b: { // kmap
								   // The size is not always accurate (e.g. GalleyAttack.amb, where a name is a byte longer than it allows
								   // for), so the chunk is stepped over by its contents
						CheckRange(ambBytes, offset, 20);
						var varName = GetNullTerminatedString(ambBytes, offset + 20, ambBytes.Length);
						int countOffset = offset + 20 + varName.size + 1;
						CheckRange(ambBytes, countOffset, 8);
						var kmap = new KmapChunk() {
							size = BitConverter.ToInt32(ambBytes, offset + 4), // does not count itself or the header tag
							unknownFlag1 = GetFlag(ambBytes[offset + 8], 0),
							unknownFlag2 = GetFlag(ambBytes[offset + 8], 1),
							unknownInt1 = BitConverter.ToInt32(ambBytes, offset + 12),
							unknownInt2 = BitConverter.ToInt32(ambBytes, offset + 16),
							varName = varName.text,
							itemCount = BitConverter.ToInt32(ambBytes, countOffset),
							dataSize = BitConverter.ToInt32(ambBytes, countOffset + 4),
						};
						// Each item is at least its three values and a terminating null
						if (kmap.itemCount < 0 || kmap.itemCount > (ambBytes.Length - countOffset) / 13) {
							throw new InvalidDataException($"Invalid kmap item count {kmap.itemCount} at {offset} in {path}");
						}

						kmap.items = new KmapItem[kmap.itemCount];
						int itemOffset = countOffset + 8;
						for (int i = 0; i < kmap.items.Length; i++) {
							CheckRange(ambBytes, itemOffset, 12);
							var wavFile = GetNullTerminatedString(ambBytes, itemOffset + 12, ambBytes.Length);
							kmap.items[i] = new KmapItem() {
								size = 12 + wavFile.size + 1,
								unknown1 = BitConverter.ToInt32(ambBytes, itemOffset),
								unknown2 = BitConverter.ToInt32(ambBytes, itemOffset + 4),
								unknown3 = BitConverter.ToInt32(ambBytes, itemOffset + 8),
								wavFileName = wavFile.text
							};
							itemOffset += kmap.items[i].size;
						}
						this.kmapChunks.Add(kmap);
						// The items are followed by a 4 byte value (0xFA)
						offset = itemOffset + 4;
						break;
					}
				case 0x6c626c67: { // glbl
						int size = ChunkSize(ambBytes, offset, 0, "glbl");
						CheckRange(ambBytes, offset + 8, 16);
						var glbl = new GlblChunk() {
							size = size,
							dataSize = BitConverter.ToInt32(ambBytes, offset + 8),
							unknownInt1 = BitConverter.ToInt32(ambBytes, offset + 12),
							unknownInt2 = BitConverter.ToInt32(ambBytes, offset + 16),
							terminated = GetBytes(ambBytes, offset + 20, 4),
						};
						this.glblChunk = glbl;
						offset += size + HEADER_SIZE;
						break;
					}
				// start of Midi section
				case 0x6468544d: { // MThd
						CheckRange(ambBytes, offset, MTHD_SIZE);
						var midi = new MidiData() {
							headerSize = GetInt32FromBigEndian(ambBytes, offset + 4),
							midiFormat = GetInt16FromBigEndian(ambBytes, offset + 8),
							trackCount = GetInt16FromBigEndian(ambBytes, offset + 10),
							ticksPerQuarterNote = GetInt16FromBigEndian(ambBytes, offset + 12),
						};
						// The header's length doesn't count the tag and length fields; it is 6 in standard MIDI files, but a
						// longer header must be stepped over whole.
						if (midi.headerSize < MTHD_SIZE - HEADER_SIZE || midi.headerSize > ambBytes.Length - offset - HEADER_SIZE) {
							throw new InvalidDataException($"Invalid MIDI header size {midi.headerSize} at {offset} in {path}");
						}
						this.midiData = midi;
						offset += HEADER_SIZE + midi.headerSize;
						break;
					}
				case 0x6b72544d: { // MTrk
						if (this.midiData == null) {
							throw new InvalidDataException($"MIDI track before the MIDI header at {offset} in {path}");
						}
						var trackSize = GetInt32FromBigEndian(ambBytes, offset + 4);
						if (trackSize < 0 || trackSize > ambBytes.Length - offset - HEADER_SIZE) {
							throw new InvalidDataException($"Invalid MIDI track size {trackSize} at {offset} in {path}");
						}
						offset += HEADER_SIZE;
						soundTracks.Add(ParseTrack(ambBytes, offset, offset + trackSize, path));
						offset += trackSize;
						break;
					}
				default:
					throw new InvalidDataException($"Unknown header: 0x{header:x} ({header}) at offset {offset} in {path}");
			}
		}

		if (this.midiData != null) {
			this.midiData.soundTracks = soundTracks.ToArray();
		}
	}

	// The size of the chunk at offset, which must be at least minSize and fit in the file
	private static int ChunkSize(byte[] bytes, int offset, int minSize, string name) {
		int size = BitConverter.ToInt32(bytes, offset + 4);
		if (size < minSize || size > bytes.Length - offset - HEADER_SIZE) {
			throw new InvalidDataException($"Invalid {name} chunk size {size} at {offset}");
		}
		return size;
	}

	private static void CheckRange(byte[] bytes, int offset, int count) {
		if (offset < 0 || offset > bytes.Length - count) {
			throw new InvalidDataException($"AMB data at {offset} runs past the end of the file");
		}
	}

	// Parses the MIDI track events in bytes[offset .. end). Event times are the time since the start of the track.
	private SoundTrack ParseTrack(byte[] ambBytes, int offset, int end, string path) {
		var soundTrack = new SoundTrack() { };
		List<ControlChangeEvent> controlChangeEvents = new List<ControlChangeEvent>();
		int time = 0;
		int runningStatus = -1;

		while (offset < end) {
			var varLenItem = ReadVariableLengthItem(ambBytes, offset, end);
			time += varLenItem.value;
			int eventOffset = offset + varLenItem.length;
			if (eventOffset >= end) {
				throw new InvalidDataException($"MIDI event at {offset} runs past the end of its track in {path}");
			}
			int status = ambBytes[eventOffset];

			if (status == 0xff) {
				// Meta event: 0xFF, its type, the length of its data (a variable length number), and the data
				if (eventOffset + 2 >= end) {
					throw new InvalidDataException($"MIDI meta event at {offset} runs past the end of its track in {path}");
				}
				int eventId = ambBytes[eventOffset + 1];
				var dataLength = ReadVariableLengthItem(ambBytes, eventOffset + 2, end);
				int dataOffset = eventOffset + 2 + dataLength.length;
				int eventSize = dataLength.value;
				if (eventSize > end - dataOffset) {
					throw new InvalidDataException($"MIDI meta event at {offset} runs past the end of its track in {path}");
				}
				switch (eventId) {
					case 0x03:
						soundTrack.TrackNameEvent = new TrackNameEvent() {
							size = eventSize,
							timeDelta = time,
							trackName = ascii.GetString(ambBytes, dataOffset, eventSize),
						};
						break;
					case 0x51:
						soundTrack.setTempoEvent = new SetTempoEvent {
							size = eventSize,
							timeDelta = time,
							// an int from 3 bytes, in big endian mode
							microsecondsPerQuarterNote = eventSize >= 3 ? GetInt24FromBigEndian(ambBytes, dataOffset) : 0,
						};
						break;
					case 0x54:
						if (eventSize < 5) {
							throw new InvalidDataException($"MIDI SMPTE offset event at {offset} is too short in {path}");
						}
						soundTrack.smpteOffsetEvent = new SMPTEOffsetEvent() {
							size = eventSize,
							timeDelta = time,
							framesPerSecond = GetFramesPerSecond(ambBytes[dataOffset]),
							hours = ExtractBits(ambBytes[dataOffset], 0, 5),
							minutes = ambBytes[dataOffset + 1],
							seconds = ambBytes[dataOffset + 2],
							frames = ambBytes[dataOffset + 3],
							subFrames = ambBytes[dataOffset + 4],
						};
						break;
					case 0x58:
						if (eventSize < 4) {
							throw new InvalidDataException($"MIDI time signature event at {offset} is too short in {path}");
						}
						soundTrack.timeSignatureEvent = new TimeSignatureEvent {
							size = eventSize,
							timeDelta = time,
							numerator = ambBytes[dataOffset],
							pow = ambBytes[dataOffset + 1],
							metronomePulse = ambBytes[dataOffset + 2],
							num32NotesPerBeat = ambBytes[dataOffset + 3],
						};
						break;
					case 0x2f:
						// end of track
						break;
					default:
						log.Debug("Skipping MIDI meta event 0x{id:x} in {path}", eventId, path);
						break;
				}
				offset = dataOffset + eventSize;
				continue;
			}

			if (status == 0xf0 || status == 0xf7) {
				// System exclusive event: its length (a variable length number) and data
				var dataLength = ReadVariableLengthItem(ambBytes, eventOffset + 1, end);
				offset = eventOffset + 1 + dataLength.length + dataLength.value;
				if (offset > end) {
					throw new InvalidDataException($"MIDI system exclusive event runs past the end of its track in {path}");
				}
				continue;
			}

			// Channel event. With running status, the status byte is left out and the previous one applies.
			int dataStart = eventOffset + 1;
			if (status < 0x80) {
				if (runningStatus < 0) {
					throw new InvalidDataException($"MIDI event without a status at {offset} in {path}");
				}
				status = runningStatus;
				dataStart = eventOffset;
			}
			runningStatus = status;
			var (highNibble, lowNibble) = GetNibbles((byte)status);
			int dataSize = highNibble == 0xc || highNibble == 0xd ? 1 : 2;
			if (dataStart + dataSize > end) {
				throw new InvalidDataException($"MIDI event at {offset} runs past the end of its track in {path}");
			}
			int eventLength = dataStart + dataSize - offset;
			switch (highNibble) {
				case 0x8:
					soundTrack.NoteOffEvent = new NoteOffEvent {
						size = eventLength,
						timeDelta = time,
						channelNumber = lowNibble,
						key = ambBytes[dataStart],
						velocity = ambBytes[dataStart + 1],
					};
					break;
				case 0x9:
					soundTrack.NoteOnEvent = new NoteOnEvent {
						size = eventLength,
						timeDelta = time,
						channelNumber = lowNibble,
						key = ambBytes[dataStart],
						velocity = ambBytes[dataStart + 1],
					};
					break;
				case 0xb:
					controlChangeEvents.Add(new ControlChangeEvent {
						size = eventLength,
						timeDelta = time,
						channelNumber = lowNibble,
						controllerNumber = ambBytes[dataStart],
						value = ambBytes[dataStart + 1],
					});
					break;
				case 0xc:
					soundTrack.programChangeEvent = new ProgramChangeEvent {
						size = eventLength,
						timeDelta = time,
						channelNumber = lowNibble,
						programNumber = ambBytes[dataStart],
					};
					break;
				default:
					// key pressure, channel pressure, pitch bend: not used
					break;
			}
			offset = dataStart + dataSize;
		}

		soundTrack.controlChangeEvents = controlChangeEvents;
		return soundTrack;
	}

	// a nibble is half a byte
	// this returns the two nibbles, read from left to right
	// so, oxB3 will return
	// high : B(11 in decimal) and low : 3
	private (int highNibble, int lowNibble) GetNibbles(byte bite) {
		var high = (byte)(bite >> 4);
		var low = (byte)(bite & 0x0F);
		return (high, low);
	}

	private float GetFramesPerSecond(byte bite) {
		bool a = GetFlag(bite, 5);
		bool b = GetFlag(bite, 6);

		if (a && b) return 30f;
		if (a && !b) return 29.97f;
		if (!a && b) return 25f;

		// !a && !b
		return 24f;
	}

	private int ExtractBits(byte value, int startBit, int bitCount) {
		int mask = (1 << bitCount) - 1;
		return (value >> startBit) & mask;
	}

	// Returns the length in bytes (excluding the terminating 0x00) and the UTF-8 decoded string, which must end before end
	private (int size, string text) GetNullTerminatedString(byte[] bytes, int offset, int end) {
		if (offset < 0 || offset >= end) {
			throw new InvalidDataException($"AMB string at {offset} is past the end of its data");
		}
		int size = bytes.AsSpan(offset, end - offset).IndexOf((byte)0x00);
		if (size < 0) {
			throw new InvalidDataException($"AMB string at {offset} isn't terminated");
		}
		return (size, System.Text.Encoding.UTF8.GetString(bytes, offset, size));
	}

	// Same as bytes.Skip(offset).Take(count).ToArray(): up to count bytes, fewer at the end of the data
	private static byte[] GetBytes(byte[] bytes, int offset, int count) {
		offset = Math.Clamp(offset, 0, bytes.Length);
		return bytes.AsSpan(offset, Math.Min(count, bytes.Length - offset)).ToArray();
	}

	private static ReadOnlySpan<byte> BigEndianBytes(byte[] bytes, int offset, int count) {
		CheckRange(bytes, offset, count);
		return new ReadOnlySpan<byte>(bytes, offset, count);
	}

	private bool GetFlag(byte b, int index) {
		return (b & (1 << index)) != 0;
	}

	private int GetInt32FromBigEndian(byte[] bytes, int offset) {
		return BinaryPrimitives.ReadInt32BigEndian(BigEndianBytes(bytes, offset, 4));
	}
	private int GetInt24FromBigEndian(byte[] bytes, int offset) {
		ReadOnlySpan<byte> b = BigEndianBytes(bytes, offset, 3);
		int value = b[0] << 16 | b[1] << 8 | b[2];
		return value;
	}
	private short GetInt16FromBigEndian(byte[] bytes, int offset) {
		return BinaryPrimitives.ReadInt16BigEndian(BigEndianBytes(bytes, offset, 2));
	}

	// A MIDI variable length number, 7 bits per byte, most significant first, of at most 4 bytes that must end before end
	private VarLenItem ReadVariableLengthItem(byte[] bytes, int offset, int end) {
		int val = 0;
		for (int len = 0; len < 4; len++) {
			if (offset + len >= end) {
				break;
			}
			byte b = bytes[offset + len];
			val = (val << 7) | (b & 0x7f);
			if ((b & 0x80) == 0) {
				return new VarLenItem(len + 1, val);
			}
		}
		throw new InvalidDataException($"Invalid MIDI variable length number at {offset}");
	}
}

// In Prgm, Kmap, and Glbl chunks, integers are little endian and strings are null terminated.

// tag prgm
public class PrgmChunk {
	public int size;
	public int index;

	public bool randomizePlaybackSpeed;
	public bool randomizeVolume;

	public int maxRandomSpeed;
	public int minRandomSpeed;
	public int maxRandomVolume;
	public int minRandomVolume;

	public string effectName;
	public string varName; // matches KmapChunk varName
}

public class KmapItem {
	public int size;
	public int unknown1;
	public int unknown2;
	public int unknown3;

	public string wavFileName;
}

// tag kmap
public class KmapChunk {
	public int size;

	public bool unknownFlag1;
	public bool unknownFlag2;

	public int unknownInt1;
	public int unknownInt2;

	public string varName; // matches PrgmChunk varName

	public int itemCount;
	public int dataSize;

	public KmapItem[] items;
}

// tag glbl
public class GlblChunk {
	public int size;
	public int dataSize;
	// I don't know what this is, but instead of having an array of bytes
	// I will break it up to 2 ints + byte[4] array
	public int unknownInt1;
	public int unknownInt2;
	public byte[] terminated; // 0xCDCDCDCD (always?)
}

// Midi file integers are big endian and strings are not null-terminated

// tag MThd
public class MidiData {
	public int headerSize; // 6 for standard midi files
	public short midiFormat; // 0, 1 or 2, in our case it's always 1
	public short trackCount; // Always >= 2 and <= 13 in our case
	public short ticksPerQuarterNote; // “Division” in the Midi spec. All AMBs in Civ 3 use “metric time”, i.e., this field specifies the length of a quarter note in delta time ticks
									  // The first track contains no sound data, just info about the tempo
	public SoundTrack[] soundTracks;
}

public class SoundTrack {
	public TrackNameEvent TrackNameEvent;
	public SMPTEOffsetEvent smpteOffsetEvent; // only on 1st track (info track)
	public TimeSignatureEvent timeSignatureEvent; // only on 1st track (info track)
	public SetTempoEvent setTempoEvent; // only on 1st track (info track)
	public List<ControlChangeEvent> controlChangeEvents;
	public ProgramChangeEvent programChangeEvent;
	public NoteOnEvent NoteOnEvent;
	public NoteOffEvent NoteOffEvent;

	public bool IsInfoTrack() {
		return this.smpteOffsetEvent != null;
	}
}

/* Midi Meta events 0xff*/

// 0x03
public sealed class TrackNameEvent : MidiEvent {
	public string trackName;
}

// 0x54
public sealed class SMPTEOffsetEvent : MidiEvent {
	public float framesPerSecond;
	public int hours;
	public int minutes;
	public int seconds;
	public int frames;
	public int subFrames;
}

// 0x58
public sealed class TimeSignatureEvent : MidiEvent {
	public int numerator; // is the numerator of the time signature and has values between 0x00 and 0xFF 
	public int pow; // is the power to which the number 2 must be raised to obtain the time signature denominator
	public int metronomePulse; // defines a metronome pulse in terms of the number of MIDI clock ticks per click
	public int num32NotesPerBeat; // defines the number of 32nd notes per beat
}

// 0x51
public sealed class SetTempoEvent : MidiEvent {
	public int microsecondsPerQuarterNote;
}

/* Midi Events */

// 0x8n
public sealed class NoteOffEvent : MidiEvent {
	public int channelNumber;
	public int key;
	public int velocity;
}

// 0x9n
public sealed class NoteOnEvent : MidiEvent {
	public int channelNumber;
	public int key;
	public int velocity;
}

// 0xBn
public sealed class ControlChangeEvent : MidiEvent {
	public int channelNumber;
	public int controllerNumber;
	public int value;
}

// 0xCn
public sealed class ProgramChangeEvent : MidiEvent {
	public int channelNumber;
	public int programNumber;
}

public abstract class MidiEvent {
	public int size;
	public int timeDelta;
}
