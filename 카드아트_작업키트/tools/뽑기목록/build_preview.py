import sys, pathlib, re, json, hashlib

root = pathlib.Path(sys.argv[1])
out_dir = pathlib.Path(sys.argv[2])
kit = root / "카드아트_작업키트"

def rd(p): return p.read_bytes().decode("utf-8").replace("\r\n", "\n")

# 진행표 완료 상태
done = {}
for l in rd(kit / "0_진행표.md").split("\n"):
    m = re.match(r"^\| \[(.)\] \|.*`([^`]+\.png)`", l)
    if m: done[m.group(2)] = m.group(1).lower() == "x"

ORDER = [
 ("1_불/1_노말-레어", "불", "노말 · 레어", "char"),
 ("1_불/2_유니크-레전더리", "불", "유니크 · 레전더리", "char"),
 ("2_물/1_노말-레어", "물", "노말 · 레어", "char"),
 ("2_물/2_유니크-레전더리", "물", "유니크 · 레전더리", "char"),
 ("3_땅/1_노말-레어", "땅", "노말 · 레어", "char"),
 ("3_땅/2_유니크-레전더리", "땅", "유니크 · 레전더리", "char"),
 ("4_공기/1_노말-레어", "공기", "노말 · 레어", "char"),
 ("4_공기/2_유니크-레전더리", "공기", "유니크 · 레전더리", "char"),
 ("5_무속성/1_노말-레어", "무속성", "노말 · 레어", "char"),
 ("5_무속성/2_유니크-레전더리", "무속성", "유니크 · 레전더리", "char"),
 ("9_수퍼레어", "수퍼레어", "5속성 · 한 대화", "char"),
 ("6_배경/1_공용-노말레어", "배경", "공용 (노말 · 레어)", "bg"),
 ("6_배경/2_수퍼레어", "배경", "수퍼레어", "bg"),
 ("6_배경/3_유니크-레전더리", "배경", "유니크 · 레전더리", "bg"),
 ("7_알", "알", "알 (배경까지 한 장)", "bg"),
 ("8_필드", "필드", "필드 (한 장 그림)", "bg"),
]

def tier_of(cid):
    m = re.search(r"[-_](N|R|SR|U|L)\d?(?:[-_]|$)", cid)
    if cid.startswith("BG_"): return "NR"
    return m.group(1) if m else ""

setups = {}
groups = []
for rel, elem, label, kind in ORDER:
    d = kit / rel
    if not d.exists(): d = kit / rel.replace("1_노말-레어", "1_노말-수퍼레어")
    t = rd(d / "프롬프트.txt")
    lines = t.split("\n")
    # 첨부 안내
    attach_notes = [l.strip() for l in lines[:12] if l.strip().startswith(("첨부", "수퍼레어(SR)", "품질 참고", "(기준"))]
    anchors = sorted(p.name for p in (d / "기준그림").glob("*")) if (d / "기준그림").exists() else []
    # 세션 설정
    sep = "=" * 60
    idx = [i for i, l in enumerate(lines) if l.strip() == sep]
    s_start = idx[1] + 1; s_end = idx[2]
    setup = "\n".join(lines[s_start:s_end]).strip()
    h = hashlib.md5(setup.encode()).hexdigest()[:8]
    setups[h] = setup
    cards = []
    for i, l in enumerate(lines[idx[3] + 1:], start=idx[3] + 1):
        if not l.startswith("--- "): continue
        body = []
        for n in lines[i + 1:]:
            if n.startswith("--- ") or (n.strip() == "" and body): break
            if n.strip(): body.append(n)
        prompt = "\n".join(body).strip()
        m = re.match(r"^--- (\S+) (.+?)  →  저장: (\S+)\s+\[(.*?)\]\s*(?:※\s*(.*))?$", l)
        note = ""
        if m:
            cid, name, fn, status, note = m.groups(); note = note or ""
        else:
            m2 = re.match(r"^--- (\S+\.png)\s+\((.*)\)\s*$", l)
            if not m2:
                print("BAD", rel, repr(l)); continue
            fn, name = m2.group(1), m2.group(2); cid = fn[:-4]; status = ""
        cards.append(dict(id=cid, name=name, file=fn, status=status, note=note, prompt=prompt,
                          tier=tier_of(cid), done=done.get(fn, False)))
    groups.append(dict(key=rel, elem=elem, label=label, kind=kind, setup=h,
                       attach=attach_notes, anchors=anchors, cards=cards))

out_dir.mkdir(parents=True, exist_ok=True)
data = dict(setups=setups, groups=groups)
(out_dir / "data.json").write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
print("groups", len(groups), "cards", sum(len(g["cards"]) for g in groups),
      "setups", len(setups), "bytes", (out_dir / "data.json").stat().st_size)
