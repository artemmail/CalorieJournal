# fix_force.py
import os
from pathlib import Path

# Какие расширения правим (добавил .html/.ts/.scss под Angular)
EXTS = {
    ".html", ".htm", ".ts", ".tsx",
    ".css", ".scss",
    ".txt", ".md", ".json", ".xml", ".ini", ".yml", ".yaml", ".csv", ".tsv", ".log",
}

# Создавать .bak перед заменой? Если не нужно — оставьте False
MAKE_BACKUP = False

MARKERS = ("Р", "Ð")  # характерные признаки кракозябр

def count_cyrillic(s: str) -> int:
    return sum(1 for ch in s if "\u0400" <= ch <= "\u04FF")

def fix_text(text: str) -> str | None:
    """
    Насильная починка уже испорченного текста (mojibake),
    пробуем два стандартных пути и берём тот, где больше кириллицы.
    """
    cand = []
    # Вариант 1: utf-8 прочитали как cp1251 -> encode('cp1251')->decode('utf-8')
    try:
        a = text.encode("cp1251", "ignore").decode("utf-8", "ignore")
        cand.append(a)
    except Exception:
        pass
    # Вариант 2: utf-8 прочитали как latin1 -> encode('latin1')->decode('utf-8')
    try:
        b = text.encode("latin1", "ignore").decode("utf-8", "ignore")
        cand.append(b)
    except Exception:
        pass
    if not cand:
        return None
    best = max(cand, key=count_cyrillic)
    # Принимаем, только если стало кириллицы больше
    if count_cyrillic(best) > count_cyrillic(text):
        return best
    return None

def process_file(p: Path):
    try:
        raw = p.read_bytes()
    except Exception as e:
        print(f"[skip read] {p}: {e}")
        return

    # 1) Сначала пробуем как UTF-8 (обычно ваши файлы уже UTF-8, но с кракозябрами внутри)
    try:
        txt = raw.decode("utf-8", "strict")
    except UnicodeDecodeError:
        txt = None

    if txt is not None:
        # Если в тексте видим маркеры, насильно чиним
        if any(m in txt for m in MARKERS):
            fixed = fix_text(txt)
            if fixed and fixed != txt:
                if MAKE_BACKUP:
                    p.with_suffix(p.suffix + ".bak").write_bytes(raw)
                p.write_text(fixed, encoding="utf-8", newline="")
                print(f"[fixed mojibake] {p}")
                return
        # Если маркеров нет — оставляем как есть
        return

    # 2) Если не вышло как UTF-8, возможно исходник в cp1251 — переводим в UTF-8
    try:
        cp = raw.decode("cp1251", "strict")
        if count_cyrillic(cp) >= 3:
            if MAKE_BACKUP:
                p.with_suffix(p.suffix + ".bak").write_bytes(raw)
            p.write_text(cp, encoding="utf-8", newline="")
            print(f"[converted cp1251->utf8] {p}")
            return
    except UnicodeDecodeError:
        pass

    # 3) На крайний случай: latin1 как исходник + спасение
    l1 = raw.decode("latin1", "replace")
    fixed = fix_text(l1)
    if fixed and count_cyrillic(fixed) >= 3:
        if MAKE_BACKUP:
            p.with_suffix(p.suffix + ".bak").write_bytes(raw)
        p.write_text(fixed, encoding="utf-8", newline="")
        print(f"[fixed latin1 mojibake] {p}")

def main():
    for root, _, files in os.walk("."):
        for name in files:
            p = Path(root) / name
            if p.suffix.lower() in EXTS and not p.name.startswith("~$"):
                process_file(p)

if __name__ == "__main__":
    main()
