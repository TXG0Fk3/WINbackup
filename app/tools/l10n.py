"""Traduções do WINbackup (app/WINbackup/Strings/en.json e es.json).

    python app/tools/l10n.py add arquivo.txt   junta traduções no formato "português|english|español", uma por linha
    python app/tools/l10n.py missing           lista textos do código (L.T/L.F/L.N e {s:T K='…'}) sem tradução em inglês

No arquivo de entrada, \\n vira quebra de linha e \\\\ vira uma barra invertida.
"""
import glob
import html
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')
APP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'WINbackup')
STRINGS = os.path.join(APP, 'Strings')
LANGS = ('en', 'es')
STR = r'"((?:[^"\\]|\\.)*)"'


def load():
    return {l: json.load(open(os.path.join(STRINGS, f'{l}.json'), encoding='utf-8')) for l in LANGS}


def save(maps):
    for l, d in maps.items():
        with open(os.path.join(STRINGS, f'{l}.json'), 'w', encoding='utf-8') as fh:
            json.dump(dict(sorted(d.items())), fh, ensure_ascii=False, indent=1)


def unescape(s):
    return re.sub(r'\\(\\|n)', lambda m: '\n' if m.group(1) == 'n' else '\\', s)


def holes(s):
    return sorted(h.split(':')[0].split(',')[0] for h in re.findall(r'\{(\d[^}]*)\}', s))


def add(path):
    maps = load()
    problems = 0
    for n, line in enumerate(open(path, encoding='utf-8'), 1):
        line = line.rstrip('\n')
        if not line.strip():
            continue
        parts = [unescape(p) for p in line.split('|')]
        if len(parts) != 1 + len(LANGS):
            print(f'linha {n}: esperado português|english|español')
            problems += 1
            continue
        key, *texts = parts
        for lang, text in zip(LANGS, texts):
            # editores costumam cortar o espaço do fim da linha: devolve o espaço final do original
            if key.endswith(' ') and not text.endswith(' '):
                text += ' '
            if holes(key) != holes(text):
                print(f'linha {n} ({lang}): lacunas diferentes: {key!r} -> {text!r}')
                problems += 1
                continue
            if text == key:
                maps[lang].pop(key, None)
            else:
                maps[lang][key] = text
    save(maps)
    print(f'en {len(maps["en"])} · es {len(maps["es"])} · problemas {problems}')


def used_keys():
    unescape_cs = lambda s: re.sub(r'\\(.)', lambda m: {'n': '\n', 't': '\t', '"': '"', '\\': '\\'}.get(m.group(1), m.group(0)), s)
    keys = set()
    for f in glob.glob(os.path.join(APP, '**', '*.xaml'), recursive=True):
        if os.sep + 'obj' + os.sep in f or os.sep + 'bin' + os.sep in f:
            continue
        for m in re.finditer(r"\{s:T K='([^']*)'\}", open(f, encoding='utf-8').read()):
            keys.add(html.unescape(m.group(1)).replace('\\\\', '\\'))
    for f in glob.glob(os.path.join(APP, '**', '*.cs'), recursive=True):
        if os.sep + 'obj' + os.sep in f or os.sep + 'bin' + os.sep in f:
            continue
        src = open(f, encoding='utf-8').read()
        for m in re.finditer(r'L\.(?:T|F)\(' + STR, src):
            keys.add(unescape_cs(m.group(1)))
        for m in re.finditer(r'L\.N\([^,]+,\s*' + STR + r',\s*' + STR, src):
            keys.update(unescape_cs(g) for g in m.groups())
    return {k for k in keys if re.search(r'[A-Za-zÀ-ÿ]', k)}


def missing():
    en = load()['en']
    for k in sorted(used_keys()):
        if k not in en:
            print(repr(k))


if __name__ == '__main__':
    if len(sys.argv) >= 3 and sys.argv[1] == 'add':
        add(sys.argv[2])
    elif len(sys.argv) >= 2 and sys.argv[1] == 'missing':
        missing()
    else:
        print(__doc__)
