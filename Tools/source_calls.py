"""Read balanced C# authoring calls without splitting commas inside strings/vectors."""
import json
import re


def arguments(text):
    result = []
    start = depth = 0
    quoted = escape = False
    for i, c in enumerate(text):
        if quoted:
            if escape: escape = False
            elif c == '\\': escape = True
            elif c == '"': quoted = False
        elif c == '"': quoted = True
        elif c in '([{': depth += 1
        elif c in ')]}': depth -= 1
        elif c == ',' and depth == 0:
            result.append(text[start:i].strip())
            start = i + 1
    return result + [text[start:].strip()]


def calls(body, name):
    for m in re.finditer(r'\b' + name + r'\(', body):
        start = m.end()
        depth = 1
        quoted = escape = False
        for i in range(start, len(body)):
            c = body[i]
            if quoted:
                if escape: escape = False
                elif c == '\\': escape = True
                elif c == '"': quoted = False
            elif c == '"': quoted = True
            elif c == '(': depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    yield arguments(body[start:i])
                    break


def literal(text):
    return json.loads(text)
