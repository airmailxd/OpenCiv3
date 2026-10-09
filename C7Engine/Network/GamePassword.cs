using System;
using System.Security.Cryptography;
using System.Text;

namespace C7Engine.Network;

// A LAN game's password, which guests must give to join. The host never
// keeps the password itself, only a verifier made from it with a salt of the
// game's own, so that its autosave doesn't give it away. A guest that hasn't
// come back to its seats with its token is asked for the password: the host
// sends the salt and a nonce of its own, and the guest answers with the
// nonce signed with the verifier it makes from the password. The password
// never crosses the network, so someone watching a LAN connection (which,
// unlike one through a relay, isn't encrypted) can't read it or replay the
// answer; they could still try to guess it from what they saw.
//
// The verifier is all a guest needs to answer, so anyone who has it can join
// as well as with the password itself: the host's autosave (see
// LanResumeInfo) must be kept as private as the password. It doesn't give
// away the password, which may be used elsewhere.
public static class GamePassword {
	// Guests from one address may get the password wrong this many times
	// before the host hangs up on them and turns the address away for a
	// while: the first lockout's length, doubling each time after, up to
	// the longest.
	public const int MaxWrongAttempts = 5;
	public static readonly TimeSpan FirstLockout = TimeSpan.FromSeconds(30);
	public static readonly TimeSpan MaxLockout = TimeSpan.FromMinutes(30);

	// The longest password the lobby takes.
	public const int MaxLength = 64;

	// Making a verifier takes long enough to slow down guessing.
	private const int Iterations = 100_000;

	public static string NewSalt() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

	public static string NewNonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

	// What the host keeps of the password.
	public static string Verifier(string password, string salt) {
		byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password ?? ""), Convert.FromHexString(salt),
			Iterations, HashAlgorithmName.SHA256, 32);
		return Convert.ToHexString(key);
	}

	// A guest's answer to the host's nonce, from the verifier.
	public static string Proof(string verifier, string nonce) {
		return Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(verifier), Encoding.UTF8.GetBytes(nonce)));
	}

	// Whether the answer is right, taking as long whether it is or not.
	public static bool Check(string verifier, string nonce, string proof) {
		if (verifier == null || nonce == null || proof == null) {
			return false;
		}
		byte[] expected = Encoding.ASCII.GetBytes(Proof(verifier, nonce));
		return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(proof.ToUpperInvariant()));
	}
}
