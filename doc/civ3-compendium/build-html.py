# Builds site/compendium.html, the web page of this compendium, from its
# Markdown files. Run it from anywhere after changing the compendium:
#   python doc/civ3-compendium/build-html.py
# It needs markdown-it-py, which reads Markdown as GitHub does
# (python -m pip install markdown-it-py mdit-py-plugins).
import html
import re
from pathlib import Path

from markdown_it import MarkdownIt
from mdit_py_plugins.anchors import anchors_plugin

HERE = Path(__file__).resolve().parent
OUT = HERE.parent.parent / "site" / "compendium.html"

SECTIONS = [
	("01-combat-and-units.md", "combat"),
	("02-city-economy.md", "economy"),
	("03-happiness-and-culture.md", "culture"),
	("04-science-and-wonders.md", "science"),
	("05-governments.md", "governments"),
	("06-diplomacy-and-espionage.md", "diplomacy"),
	("07-map-terrain-and-workers.md", "map"),
	("08-ai-difficulty-and-victory.md", "ai"),
	("audit-2026-10.md", "engine"),
]
ANCHOR = {name: anchor for name, anchor in SECTIONS}

STYLE = """
	:root {
		color-scheme: light dark;
		--bg: #ffffff;
		--text: #1f2328;
		--muted: #59636e;
		--line: #d1d9e0;
		--code: #f6f8fa;
		--link: #0969da;
	}
	@media (prefers-color-scheme: dark) {
		:root:not([data-theme="light"]) {
			--bg: #0d1117;
			--text: #e6edf3;
			--muted: #9198a1;
			--line: #3d444d;
			--code: #161b22;
			--link: #4493f8;
		}
	}
	:root[data-theme="dark"] {
		--bg: #0d1117;
		--text: #e6edf3;
		--muted: #9198a1;
		--line: #3d444d;
		--code: #161b22;
		--link: #4493f8;
	}
	body { background: var(--bg); color: var(--text); font: 17px/1.6 system-ui, sans-serif; margin: 0; }
	main { max-width: 52rem; margin: 0 auto; padding: 2.5rem 16px 4rem; }
	a { color: var(--link); overflow-wrap: anywhere; }
	h1 { font-size: 1.6rem; margin: 1rem 0 0.25rem; }
	h2 { font-size: 1.4rem; margin: 3rem 0 0.5rem; padding-top: 1.5rem; border-top: 1px solid var(--line); }
	h3 { font-size: 1.15rem; margin: 2rem 0 0.25rem; }
	h4, h5 { font-size: 1rem; margin: 1.25rem 0 0.25rem; }
	ol, ul { padding-left: 1.4rem; }
	li { margin: 0.3rem 0; }
	code { background: var(--code); padding: 0.1em 0.3em; border-radius: 4px; font-size: 0.9em; overflow-wrap: anywhere; }
	pre { background: var(--code); padding: 0.75rem; border-radius: 6px; overflow-x: auto; }
	pre code { padding: 0; }
	.table { overflow-x: auto; margin: 1rem 0; }
	table { border-collapse: collapse; font-size: 0.92rem; }
	th, td { border: 1px solid var(--line); padding: 0.35rem 0.6rem; text-align: left; vertical-align: top; }
	th { background: var(--code); }
	.muted { color: var(--muted); }
	nav ul { columns: 2 16rem; }
	.top { font-size: 0.9rem; }
"""


def slugify(prefix):
	def slug(title):
		return prefix + "-" + re.sub(r"[^\w\s-]", "", title.lower()).strip().replace(" ", "-")
	return slug


def convert(text, prefix):
	md = MarkdownIt("commonmark", {"html": False}).enable("table").use(anchors_plugin, max_level=6, slug_func=slugify(prefix))
	body = md.render(text)
	# Links between the compendium's files become links within the page.
	body = re.sub(r'href="([\w-]+\.md)(#[^"]*)?"', lambda m: f'href="#{ANCHOR.get(m.group(1), "")}"', body)
	# Each file's headings sit one level under the page's.
	body = re.sub(r"<(/?)h([1-5])", lambda m: f"<{m.group(1)}h{int(m.group(2)) + 1}", body)
	body = body.replace("<table>", '<div class="table"><table>').replace("</table>", "</table></div>")
	return body


def title_of(text):
	return re.search(r"^# (.+)$", text, re.M).group(1).strip()


readme = (HERE / "README.md").read_text(encoding="utf-8")
intro = readme.split("\n| Section")[0]
intro = re.sub(r"^# .*\n", "", intro).strip()

parts = []
nav = []
for name, anchor in SECTIONS:
	text = (HERE / name).read_text(encoding="utf-8")
	title = title_of(text)
	if name.startswith("audit"):
		title = "Appendix: where the game still differs"
	text = re.sub(r"^# .*\n", "", text, count=1)
	nav.append(f'\t\t\t<li><a href="#{anchor}">{html.escape(title)}</a></li>')
	parts.append(f'\t<section id="{anchor}">\n\t<h2>{html.escape(title)}</h2>\n{convert(text, anchor)}\n'
		f'\t<p class="top"><a href="#top">Back to the top</a></p>\n\t</section>')

page = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Civ3 rules compendium</title>
<style>{STYLE}</style>
</head>
<body>
<main id="top">
	<p><a href="./">&larr; Back to the downloads</a></p>
	<h1>Civ3 rules compendium</h1>
{convert(intro, "intro")}
	<nav aria-label="Sections">
		<ul>
{chr(10).join(nav)}
		</ul>
	</nav>
{chr(10).join(parts)}
</main>
</body>
</html>
"""
OUT.write_text(page, encoding="utf-8", newline="\n")
print(f"wrote {OUT} ({len(page) // 1024} KB)")
