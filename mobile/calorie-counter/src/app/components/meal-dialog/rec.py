# fix_strong.py
import os
from pathlib import Path

EXTS = {
    ".html", ".htm", ".ts", ".tsx",
    ".css", ".scss",
    ".txt", ".md", ".json", ".xml", ".ini", ".yml", ".yaml", ".csv", ".tsv", ".log",
}
MAKE_BACKUP = False

# Частые маркеры кракозябр (и латинские, и квазикириллица)
MARKERS = set("РСÐÑÂÒÓ")
GARBAGE  = set("‚„…•—™€›‹œš›")

def score_badness(s: str) -> int:
    # Чем БОЛЬШЕ, тем ХУЖЕ: считаем маркеры и «мусор»
    m = sum(s.count(ch) for ch in MARKERS)
    g = sum(s.count(ch) for ch in GARBAGE)
    # накажем также за длинные цепочки Р/С/Ð/Ñ
    runs = 0
    run = 0
    for ch in s:
        if ch in MARKERS:
            run += 1
            if run >= 2: runs += 1
        else:
            run = 0
    return m*3 + g*4 + runs

def try_fix(text: str):
    cands = []
    # тип 1: UTF-8 прочитан как cp1251 -> encode('cp1251')->decode('utf-8')
    try:
        a = text.encode("cp1251", "ignore").decode("utf-8", "ignore")
        cands.append(("cp1251->utf8", a))
    except Exception:
        pass
    # тип 2: UTF-8 прочитан как latin1 -> encode('latin1')->decode('utf-8')
    try:
        b = text.encode("latin1", "ignore").decode("utf-8", "ignore")
        cands.append(("latin1->utf8", b))
    except Exception:
        pass
    if not cands:
        return None, None

    base_bad = score_badness(text)
    best = min(cands, key=lambda kv: score_badness(kv[1]))
    best_bad = score_badness(best[1])

    # Примем починку, если «плохость» явно уменьшилась
    if best_bad <= base_bad - 5:
        return best[0], best[1]
    return None, None

def process_file(p: Path):
    try:
        raw = p.read_bytes()
    except Exception as e:
        print(f"[skip read] {p}: {e}")
        return

    # Попробуем как UTF-8 (в Angular/веб это обычный случай)
    txt = None
    try:
        txt = raw.decode("utf-8", "strict")
    except UnicodeDecodeError:
        pass

    if txt is not None:
        meth, fixed = try_fix(txt)
        if fixed and fixed != txt:
            if MAKE_BACKUP:
                (p.with_suffix(p.suffix + ".bak")).write_bytes(raw)
            p.write_text(fixed, encoding="utf-8", newline="")
            print(f"[fixed mojibake] {p} via {meth}")
        return

    # Не UTF-8? Возможно, настоящий cp1251 — переведём в UTF-8.
    try:
        cp = raw.decode("cp1251", "strict")
        # даже если это «нормальный» cp1251, перепишем как UTF-8
        if MAKE_BACKUP:
            (p.with_suffix(p.suffix + ".bak")).write_bytes(raw)
        p.write_text(cp, encoding="utf-8", newline="")
        print(f"[converted cp1251->utf8] {p}")
        return
    except UnicodeDecodeError:
        pass

    # Последняя попытка: latin1 как исходник + спасение
    l1 = raw.decode("latin1", "replace")
    meth, fixed = try_fix(l1)
    if fixed:
        if MAKE_BACKUP:
            (p.with_suffix(p.suffix + ".bak")).write_bytes(raw)
        p.write_text(fixed, encoding="utf-8", newline="")
        print(f"[fixed latin1 mojibake] {p} via {meth}")

def main():
    for root, _, files in os.walk("."):
        for name in files:
            p = Path(root) / name
            if p.suffix.lower() in EXTS and not p.name.startswith("~$"):
                process_file(p)

if __name__ == "__main__":
    main()
