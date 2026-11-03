#!/usr/bin/env python3
"""graphify의 graph.json에서 프로젝트 스크립트(.cs 파일)끼리의 연결만 남긴 HTML 그래프를 만든다.

    python -I .claude/scripts/graph-scripts-view.py [graph.json] [출력.html]

- 노드 = Assets/01_Scripts 아래 .cs 파일 하나. 그 파일의 클래스·메서드 노드를 파일로 접는다.
- Vector3, Action, MonoBehaviour 같은 외부 타입과 namespace 노드는 버린다. 거대한 허브가 되어 배치를 흐리기 때문이다.
- 색은 알고리즘 커뮤니티 대신 폴더(Combat, Player, UI...)로 칠한다. 사람이 구조를 판단하기 쉽게.
- GlobalEvents 구독·발행은 graph.json에 없으므로 여기에도 나오지 않는다.
"""
import collections
import json
import os
import sys

SCRIPTS_ROOT = "Assets/01_Scripts/"
KEEP_RELATIONS = {"calls", "references", "inherits", "implements", "dispatches_to", "case_of"}
PALETTE = ["#4E79A7", "#F28E2B", "#E15759", "#76B7B2", "#59A14F", "#EDC948",
           "#B07AA1", "#FF9DA7", "#9C755F", "#BAB0AC", "#86BCB6", "#D37295"]


def folder_of(path):
    rest = path[len(SCRIPTS_ROOT):]
    return rest.split("/")[0] if "/" in rest else "(root)"


def build(graph):
    file_of = {}
    for n in graph["nodes"]:
        src = n.get("source_file") or ""
        meta = n.get("metadata") if isinstance(n.get("metadata"), dict) else {}
        if src.startswith(SCRIPTS_ROOT) and src.endswith(".cs") and meta.get("kind") != "csharp_namespace":
            file_of[n["id"]] = src

    edges = collections.defaultdict(collections.Counter)
    for e in graph["links"]:
        if e.get("relation") not in KEEP_RELATIONS:
            continue
        a, b = file_of.get(e["source"]), file_of.get(e["target"])
        if a and b and a != b:
            edges[(a, b)][e["relation"]] += 1

    files = sorted(set(file_of.values()))
    degree = collections.Counter()
    for a, b in edges:
        degree[a] += 1
        degree[b] += 1
    folders = sorted({folder_of(f) for f in files})
    color = {f: PALETTE[i % len(PALETTE)] for i, f in enumerate(folders)}

    nodes = [{
        "id": f,
        "label": os.path.splitext(os.path.basename(f))[0],
        "group": folder_of(f),
        "value": 1 + degree[f],
        "color": color[folder_of(f)],
        "title": f"{f}\n연결 {degree[f]}",
    } for f in files]
    links = [{
        "from": a, "to": b,
        "title": ", ".join(f"{r} {c}" for r, c in rel.most_common()),
        "dashes": "inherits" not in rel and "implements" not in rel and "calls" not in rel,
    } for (a, b), rel in edges.items()]
    return nodes, links, color


HTML = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>100BagBack 스크립트 그래프</title>
<script src="https://unpkg.com/vis-network@9.1.6/standalone/umd/vis-network.min.js"></script>
<style>
  :root { --bg:#14161a; --panel:#1e2127; --text:#e6e6e6; --muted:#9aa0a6; }
  html, body { height:100%; }
  body { margin:0; background:var(--bg); color:var(--text); font:13px system-ui, sans-serif; display:flex; flex-direction:column; }
  #bar { display:flex; flex-wrap:wrap; gap:6px 12px; align-items:center; padding:8px 12px; background:var(--panel); }
  #bar input { background:#2a2e35; color:var(--text); border:1px solid #3a3f47; border-radius:4px; padding:4px 8px; }
  .chip { display:inline-flex; align-items:center; gap:4px; cursor:pointer; user-select:none; }
  .chip i { width:10px; height:10px; border-radius:50%; display:inline-block; }
  .chip.off { opacity:.35; }
  #info { color:var(--muted); margin-left:auto; }
  #net { flex:1; min-height:0; }
</style></head><body>
<div id="bar"><input id="q" placeholder="스크립트 검색" size="18"><span id="legend"></span>
<span id="info">실선: 호출·상속 / 점선: 참조만 · 클릭하면 이웃만 강조 · GlobalEvents 연결은 없음</span></div>
<div id="net"></div>
<script>
const NODES = __NODES__, EDGES = __EDGES__, COLORS = __COLORS__;
const nodes = new vis.DataSet(NODES.map(n => ({...n, color:{background:n.color, border:n.color}, font:{color:"#e6e6e6"}})));
const edges = new vis.DataSet(EDGES.map((e, i) => ({...e, id:i, arrows:"to", color:{color:"#5a606a", highlight:"#ffffff"}})));
const net = new vis.Network(document.getElementById("net"), {nodes, edges}, {
  nodes:{shape:"dot", scaling:{min:6, max:40, label:{enabled:true, min:11, max:20}}},
  edges:{smooth:false, width:1},
  physics:{solver:"forceAtlas2Based", forceAtlas2Based:{gravitationalConstant:-80, springLength:140}, stabilization:{iterations:300}},
  interaction:{hover:true, tooltipDelay:120}
});
net.once("stabilizationIterationsDone", () => { net.setOptions({physics:false}); net.fit(); });
const hidden = new Set();
function apply(focus) {
  const keep = focus ? new Set([focus, ...net.getConnectedNodes(focus)]) : null;
  nodes.update(NODES.map(n => ({id:n.id, hidden:hidden.has(n.group),
    opacity: keep && !keep.has(n.id) ? 0.15 : 1})));
}
net.on("click", p => apply(p.nodes[0]));
const legend = document.getElementById("legend");
Object.entries(COLORS).forEach(([g, c]) => {
  const s = document.createElement("span"); s.className = "chip"; s.innerHTML = `<i style="background:${c}"></i>${g}`;
  s.onclick = () => { hidden.has(g) ? hidden.delete(g) : hidden.add(g); s.classList.toggle("off"); apply(); };
  legend.appendChild(s);
});
document.getElementById("q").addEventListener("keydown", e => {
  if (e.key !== "Enter") return;
  const q = e.target.value.trim().toLowerCase();
  const hit = NODES.find(n => n.label.toLowerCase().includes(q));
  if (hit) { net.selectNodes([hit.id]); net.focus(hit.id, {scale:1.4, animation:true}); apply(hit.id); }
});
</script></body></html>
"""


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    src =sys.argv[1] if len(sys.argv) > 1 else "graphify-out/graph.json"
    out = sys.argv[2] if len(sys.argv) > 2 else "graphify-out/scripts.html"
    with open(src, encoding="utf-8") as f:
        nodes, links, color = build(json.load(f))
    html = (HTML.replace("__NODES__", json.dumps(nodes, ensure_ascii=False))
                .replace("__EDGES__", json.dumps(links, ensure_ascii=False))
                .replace("__COLORS__", json.dumps(color, ensure_ascii=False)))
    with open(out, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"{out}: 스크립트 {len(nodes)}개, 연결 {len(links)}개, 폴더 {len(color)}개")


if __name__ == "__main__":
    main()
