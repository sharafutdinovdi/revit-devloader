"""Resolve repository-relative links after expanding root document snippets."""

import re
from pathlib import Path
from urllib.parse import urlsplit

from markdown import Markdown

ROOT_PAGES = {
    "README.md": "index.md",
    "CONTRIBUTING.md": "contributing.md",
    "CHANGELOG.md": "changelog.md",
    "SECURITY.md": "security.md",
}


def on_page_markdown(markdown, page, config, files):
    root = Path(config.config_file_path).parent
    source = next(
        (name for name, target in ROOT_PAGES.items() if target == page.file.src_uri),
        None,
    )
    if source:
        processor = Markdown(
            extensions=["pymdownx.snippets"],
            extension_configs={"pymdownx.snippets": {"base_path": [str(root)]}},
        )
        markdown = "\n".join(processor.preprocessors["snippet"].run(markdown.splitlines()))
        page.edit_url = f"{config.repo_url}/edit/main/{source}"
        markdown = markdown.replace('src="docs/screenshots/', 'src="screenshots/')

    def resolve(match):
        target = match.group(1)
        url = urlsplit(target)
        if url.scheme or url.netloc or not url.path:
            return match.group(0)
        path = url.path
        if source:
            if path in ROOT_PAGES:
                path = ROOT_PAGES[path]
            elif path.startswith("docs/"):
                path = path.removeprefix("docs/")
            else:
                path = f"{config.repo_url}/blob/main/{path}"
        elif path.startswith("../"):
            path = f"{config.repo_url}/blob/main/{path.removeprefix('../')}"
        suffix = (f"?{url.query}" if url.query else "") + (f"#{url.fragment}" if url.fragment else "")
        return f"]({path}{suffix})"

    return re.sub(r"\]\(([^\s)]+)\)", resolve, markdown)
