#!/usr/bin/env python3
"""Remove comments from C# files while keeping strings intact."""

from __future__ import annotations

import argparse
from pathlib import Path


def strip_csharp_comments(source: str) -> str:
    result: list[str] = []
    i = 0
    n = len(source)

    state = "code"
    interpolation_depth_stack: list[int] = []
    brace_depth = 0

    while i < n:
        c = source[i]
        nxt = source[i + 1] if i + 1 < n else ""

        if state == "code":
            if c == "/" and nxt == "/":
                i += 2
                while i < n and source[i] not in "\r\n":
                    i += 1
                continue

            if c == "/" and nxt == "*":
                i += 2
                while i < n:
                    if source[i] == "*" and i + 1 < n and source[i + 1] == "/":
                        i += 2
                        break
                    if source[i] in "\r\n":
                        result.append(source[i])
                    i += 1
                continue

            if c == "@":
                if nxt == '"':
                    result.append(c)
                    result.append(nxt)
                    i += 2
                    state = "verbatim_string"
                    continue
                if nxt == "$" and i + 2 < n and source[i + 2] == '"':
                    result.append(c)
                    result.append(nxt)
                    result.append(source[i + 2])
                    i += 3
                    state = "interpolated_verbatim_string"
                    continue

            if c == "$":
                if nxt == '"':
                    result.append(c)
                    result.append(nxt)
                    i += 2
                    state = "interpolated_string"
                    continue
                if nxt == "@" and i + 2 < n and source[i + 2] == '"':
                    result.append(c)
                    result.append(nxt)
                    result.append(source[i + 2])
                    i += 3
                    state = "interpolated_verbatim_string"
                    continue

            if c == '"':
                result.append(c)
                i += 1
                state = "string"
                continue

            if c == "'":
                result.append(c)
                i += 1
                state = "char"
                continue

            if interpolation_depth_stack:
                if c == "{":
                    brace_depth += 1
                elif c == "}":
                    if brace_depth == interpolation_depth_stack[-1]:
                        interpolation_depth_stack.pop()
                        state = "interpolated_string"
                    else:
                        brace_depth -= 1

            result.append(c)
            i += 1
            continue

        if state == "string":
            result.append(c)
            i += 1
            if c == "\\" and i < n:
                result.append(source[i])
                i += 1
            elif c == '"':
                state = "code"
            continue

        if state == "char":
            result.append(c)
            i += 1
            if c == "\\" and i < n:
                result.append(source[i])
                i += 1
            elif c == "'":
                state = "code"
            continue

        if state == "verbatim_string":
            result.append(c)
            i += 1
            if c == '"' and nxt == '"':
                result.append(nxt)
                i += 1
            elif c == '"':
                state = "code"
            continue

        if state == "interpolated_string":
            result.append(c)
            i += 1
            if c == "\\" and i < n:
                result.append(source[i])
                i += 1
            elif c == "{":
                if i < n and source[i] == "{":
                    result.append(source[i])
                    i += 1
                else:
                    interpolation_depth_stack.append(brace_depth)
                    state = "code"
            elif c == "}":
                if i < n and source[i] == "}":
                    result.append(source[i])
                    i += 1
            elif c == '"':
                state = "code"
            continue

        if state == "interpolated_verbatim_string":
            result.append(c)
            i += 1
            if c == '"' and nxt == '"':
                result.append(nxt)
                i += 1
            elif c == "{":
                if i < n and source[i] == "{":
                    result.append(source[i])
                    i += 1
                else:
                    interpolation_depth_stack.append(brace_depth)
                    state = "code"
            elif c == "}":
                if i < n and source[i] == "}":
                    result.append(source[i])
                    i += 1
            elif c == '"':
                state = "code"
            continue

    return "".join(result)


def find_comment_only_lines(source: str) -> set[int]:
    comment_lines: set[int] = set()
    lines = source.splitlines(keepends=True)
    in_block = False

    for index, line in enumerate(lines):
        i = 0
        has_comment = False
        has_code = False

        while i < len(line):
            c = line[i]
            nxt = line[i + 1] if i + 1 < len(line) else ""

            if in_block:
                has_comment = True
                if c == "*" and nxt == "/":
                    in_block = False
                    i += 2
                else:
                    i += 1
                continue

            if c.isspace():
                i += 1
                continue

            if c == "/" and nxt == "/":
                has_comment = True
                break

            if c == "/" and nxt == "*":
                has_comment = True
                in_block = True
                i += 2
                continue

            has_code = True
            break

        if has_comment and not has_code:
            comment_lines.add(index)

    return comment_lines


def remove_comment_only_leftovers(original: str, stripped: str) -> str:
    original_lines = original.splitlines(keepends=True)
    stripped_lines = stripped.splitlines(keepends=True)
    comment_lines = find_comment_only_lines(original)
    cleaned: list[str] = []

    for index, line in enumerate(stripped_lines):
        original_line = original_lines[index] if index < len(original_lines) else ""
        if index in comment_lines and not line.strip():
            continue

        if original_line != line:
            line_ending = ""
            if line.endswith("\r\n"):
                line_ending = "\r\n"
                line = line[:-2]
            elif line.endswith("\n") or line.endswith("\r"):
                line_ending = line[-1]
                line = line[:-1]
            line = line.rstrip() + line_ending

        cleaned.append(line)

    return "".join(cleaned)


def iter_targets(paths: list[Path], recursive: bool) -> list[Path]:
    targets: list[Path] = []
    for path in paths:
        if path.is_dir():
            pattern = "**/*.cs" if recursive else "*.cs"
            targets.extend(sorted(path.glob(pattern)))
        elif path.suffix.lower() == ".cs":
            targets.append(path)
    return targets


def process_file(path: Path, in_place: bool, backup: bool) -> str | None:
    original = path.read_text(encoding="utf-8-sig")
    stripped = remove_comment_only_leftovers(original, strip_csharp_comments(original))

    if not in_place:
        return stripped

    if backup:
        path.with_suffix(path.suffix + ".bak").write_text(original, encoding="utf-8")
    path.write_text(stripped, encoding="utf-8")
    return None


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Remove // and /* */ comments from C# files without touching string literals."
    )
    parser.add_argument("paths", nargs="+", type=Path, help="C# files or folders to process.")
    parser.add_argument("-i", "--in-place", action="store_true", help="Overwrite files.")
    parser.add_argument("-b", "--backup", action="store_true", help="Write .bak files before overwriting.")
    parser.add_argument("-r", "--recursive", action="store_true", help="Recurse into folders.")
    args = parser.parse_args()

    targets = iter_targets(args.paths, args.recursive)
    if not targets:
        parser.error("No .cs files found.")

    if not args.in_place and len(targets) > 1:
        parser.error("Use --in-place when processing more than one file.")

    for target in targets:
        output = process_file(target, args.in_place, args.backup)
        if output is not None:
            print(output, end="")

    if args.in_place:
        print(f"Processed {len(targets)} file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
