from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

PROJECT = Path(__file__).resolve().parent
CONFIG_PATH = PROJECT / "config.xml"
HTML_PATH = PROJECT / "index.html"


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> None:
    try:
        root = ET.parse(CONFIG_PATH).getroot()
    except (ET.ParseError, OSError) as error:
        fail(f"Unable to parse config.xml: {error}")

    content = root.find("{http://www.w3.org/ns/widgets}content")
    if content is None:
        fail("config.xml is missing the W3C content element.")

    entry_point = content.attrib.get("src", "")
    if not entry_point or not (PROJECT / entry_point).is_file():
        fail(f"Application entry point does not exist: {entry_point!r}")

    application = root.find("{http://tizen.org/ns/widgets}application")
    if application is None:
        fail("config.xml is missing tizen:application.")

    application_id = application.attrib.get("id", "")
    package_id = application.attrib.get("package", "")
    if not application_id or not package_id:
        fail("Tizen application ID and package ID must both be set.")

    if application.attrib.get("required_version") != "2.3":
        fail("The probe baseline is expected to remain Tizen 2.3 so older devices can report missing WASM support.")

    html = HTML_PATH.read_text(encoding="utf-8")
    references = re.findall(r'<script\b[^>]*?\bsrc=["\']([^"\']+)["\']', html)
    references.extend(re.findall(r'<link\b[^>]*?\bhref=["\']([^"\']+)["\']', html))

    for reference in references:
        if reference.startswith(("http://", "https://", "//", "data:")):
            fail(f"The offline probe must not load a remote resource: {reference}")
        if not (PROJECT / reference).is_file():
            fail(f"Referenced local resource does not exist: {reference}")

    required_files = ("config.xml", "index.html", "styles.css", "app.js", "build.ps1")
    for name in required_files:
        if not (PROJECT / name).is_file():
            fail(f"Required project file is missing: {name}")

    print("Tizen TV probe project structure is valid.")
    print(f"Application ID: {application_id}")
    print(f"Package ID: {package_id}")
    print(f"Local web resources checked: {len(references)}")


if __name__ == "__main__":
    main()
