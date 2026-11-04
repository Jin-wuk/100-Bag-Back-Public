#!/usr/bin/env python3
"""graphify의 graph.json에서 프로젝트 스크립트(.cs 파일)끼리의 연결만 남긴 HTML 그래프를 만든다.

    python -I .claude/scripts/graph-scripts-view.py [graph.json] [출력.html]

- 노드 = Assets/01_Scripts 아래 .cs 파일 하나. 파일을 더블클릭하면 그 파일의 메서드가 펼쳐지고,
  메서드 단위의 호출·참조가 그려진다. 다시 더블클릭하면 접힌다.
- Vector3, Action, MonoBehaviour 같은 외부 타입과 namespace 노드는 버린다. 거대한 허브가 되어 배치를 흐리기 때문이다.
- 색은 알고리즘 커뮤니티 대신 폴더(Combat, Player, UI...)로 칠한다. 사람이 구조를 판단하기 쉽게.
- GlobalEvents 구독·발행은 graph.json에 없으므로 여기에도 나오지 않는다.
"""
import json
import os
import sys

SCRIPTS_ROOT = "Assets/01_Scripts/"
RELATIONS = ["calls", "references", "inherits", "implements", "dispatches_to", "case_of"]
PALETTE = ["#4E79A7", "#F28E2B", "#E15759", "#76B7B2", "#59A14F", "#EDC948",
           "#B07AA1", "#FF9DA7", "#9C755F", "#BAB0AC", "#86BCB6", "#D37295"]


def folder_of(path):
    rest = path[len(SCRIPTS_ROOT):]
    return rest.split("/")[0] if "/" in rest else "(root)"


def build(graph):
    project = []
    for n in graph["nodes"]:
        src = n.get("source_file") or ""
        meta = n.get("metadata") if isinstance(n.get("metadata"), dict) else {}
        if src.startswith(SCRIPTS_ROOT) and src.endswith(".cs") and meta.get("kind") != "csharp_namespace":
            project.append(n)

    files = sorted({n["source_file"] for n in project})
    file_idx = {f: i for i, f in enumerate(files)}
    node_idx = {n["id"]: i for i, n in enumerate(project)}
    rel_idx = {r: i for i, r in enumerate(RELATIONS)}

    # 노드: [파일 번호, 메서드 여부, 표시 이름]. 메서드 이름은 "Class.Method()"에서 클래스 접두를 뗀다.
    nodes = []
    for n in project:
        label = n["label"]
        is_method = label.endswith(")")
        nodes.append([file_idx[n["source_file"]], 1 if is_method else 0,
                      label.rsplit(".", 1)[-1] if is_method else label])

    # 같은 파일 안의 연결도 남긴다. 펼쳤을 때 메서드끼리의 호출을 보여야 하기 때문이다.
    edges = set()
    for e in graph["links"]:
        a, b, rel = node_idx.get(e["source"]), node_idx.get(e["target"]), e.get("relation")
        if a is not None and b is not None and a != b and rel in rel_idx:
            edges.add((a, b, rel_idx[rel]))

    folders = sorted({folder_of(f) for f in files})
    colors = {g: PALETTE[i % len(PALETTE)] for i, g in enumerate(folders)}
    file_rows = [{"label": os.path.splitext(os.path.basename(f))[0], "path": f, "group": folder_of(f)} for f in files]
    return {"files": file_rows, "nodes": nodes, "edges": sorted(edges), "relations": RELATIONS, "colors": colors}


HTML = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>100BagBack 스크립트 그래프</title>
<script src="https://unpkg.com/vis-network@9.1.6/standalone/umd/vis-network.min.js"></script>
<style>
  :root { --bg:#14161a; --panel:#1e2127; --text:#e6e6e6; --muted:#9aa0a6; }
  html, body { height:100%; }
  body { margin:0; background:var(--bg); color:var(--text); font:13px system-ui, sans-serif; display:flex; flex-direction:column; }
  #bar { display:flex; flex-wrap:wrap; gap:6px 12px; align-items:center; padding:8px 12px; background:var(--panel); }
  #bar input, #bar button { background:#2a2e35; color:var(--text); border:1px solid #3a3f47; border-radius:4px; padding:4px 8px; }
  #bar button { cursor:pointer; }
  .chip { display:inline-flex; align-items:center; gap:4px; cursor:pointer; user-select:none; }
  .chip i { width:10px; height:10px; border-radius:50%; display:inline-block; }
  .chip.off { opacity:.35; }
  #info { color:var(--muted); }
  #net { flex:1; min-height:0; }
</style></head><body>
<div id="bar"><input id="q" placeholder="스크립트 검색 (Enter)" size="18"><button id="collapse">모두 접기</button><span id="legend"></span>
<span id="info">더블클릭: 메서드 펼치기·접기 · 클릭: 이웃만 강조 · 굵은 노란선: 상속·구현 / 실선: 호출 / 점선: 참조 · GlobalEvents 연결은 없음</span></div>
<div id="net"></div>
<script>
const D = __DATA__;
const REL = D.relations, STRONG = new Set(["inherits", "implements"]);
const expanded = new Set(), hidden = new Set();
const fid = f => "f" + f, mid = n => "m" + n;
const fileOf = id => id[0] === "f" ? +id.slice(1) : D.nodes[+id.slice(1)][0];

// 펼친 파일의 메서드는 자기 노드로, 나머지는 파일 노드로 모아 연결을 다시 계산한다.
function endpoint(n) {
  const [f, isMethod] = D.nodes[n];
  return expanded.has(f) && isMethod ? mid(n) : fid(f);
}
function computeEdges() {
  const map = new Map();
  for (const [a, b, r] of D.edges) {
    const from = endpoint(a), to = endpoint(b);
    if (from === to) continue;
    const key = from + ">" + to;
    if (!map.has(key)) map.set(key, {from, to, rels:{}});
    const rels = map.get(key).rels; rels[REL[r]] = (rels[REL[r]] || 0) + 1;
  }
  const out = [];
  for (const [key, e] of map) {
    const names = Object.keys(e.rels);
    const strong = names.some(n => STRONG.has(n));
    out.push({id:key, from:e.from, to:e.to, arrows:"to",
      title:names.map(n => n + " " + e.rels[n]).join(", "),
      dashes:!strong && !names.includes("calls") && !names.includes("dispatches_to"),
      width:strong ? 3 : 1,
      color:{color:strong ? "#d9b44a" : "#5a606a", highlight:"#ffffff"}});
  }
  for (const f of expanded)
    D.nodes.forEach(([nf, isMethod], n) => { if (nf === f && isMethod)
      out.push({id:"c" + n, from:fid(f), to:mid(n), length:50, dashes:[2, 4], width:1, color:{color:"#3a3f47"}}); });
  return out;
}

const degree = {};
const nodes = new vis.DataSet(D.files.map((f, i) => ({id:fid(i), label:f.label, folder:f.group, value:1,
  title:f.path, color:{background:D.colors[f.group], border:D.colors[f.group]}, font:{color:"#e6e6e6"}})));
const edges = new vis.DataSet(computeEdges());
edges.forEach(e => { degree[e.from] = (degree[e.from] || 0) + 1; degree[e.to] = (degree[e.to] || 0) + 1; });
nodes.update(D.files.map((f, i) => ({id:fid(i), value:1 + (degree[fid(i)] || 0),
  title:f.path + "\\n연결 " + (degree[fid(i)] || 0) + " · 더블클릭으로 메서드 펼치기"})));

const net = new vis.Network(document.getElementById("net"), {nodes, edges}, {
  nodes:{shape:"dot", scaling:{min:6, max:40, label:{enabled:true, min:11, max:20}}},
  edges:{smooth:false},
  physics:{solver:"forceAtlas2Based", forceAtlas2Based:{gravitationalConstant:-80, springLength:140}, stabilization:{iterations:300}},
  interaction:{hover:true, tooltipDelay:120}
});
net.once("stabilizationIterationsDone", () => { net.setOptions({physics:false}); net.fit(); });

function toggle(f) {
  const color = D.colors[D.files[f].group];
  if (expanded.has(f)) {
    expanded.delete(f);
    nodes.remove(nodes.getIds({filter:n => n.id[0] === "m" && fileOf(n.id) === f}));
  } else {
    expanded.add(f);
    const methods = D.nodes.map((n, i) => [n, i]).filter(([n]) => n[0] === f && n[1]);
    const c = net.getPosition(fid(f)), r = 60 + methods.length * 6;
    nodes.add(methods.map(([n, i], k) => {
      const a = 2 * Math.PI * k / methods.length;
      return {id:mid(i), label:n[2], folder:D.files[f].group, value:1, size:5, shape:"dot",
        x:c.x + r * Math.cos(a), y:c.y + r * Math.sin(a),
        title:D.files[f].label + "." + n[2], color:{background:"#14161a", border:color}, borderWidth:2,
        font:{color:"#c8c8c8", size:11}};
    }));
  }
  edges.clear(); edges.add(computeEdges()); apply();
  settle();
}

// 펼친 메서드가 이웃 노드와 겹치지 않게, 파일 노드는 고정하고 메서드만 잠깐 물리로 자리를 잡게 한다.
function settle() {
  const fileIds = D.files.map((_, i) => fid(i));
  nodes.update(fileIds.map(id => ({id, fixed:true})));
  net.setOptions({physics:{enabled:true}});
  net.once("stabilized", () => {
    net.setOptions({physics:{enabled:false}});
    nodes.update(fileIds.map(id => ({id, fixed:false})));
  });
  net.stabilize(150);
}
net.on("doubleClick", p => { if (p.nodes.length) toggle(fileOf(p.nodes[0])); });
document.getElementById("collapse").onclick = () => [...expanded].forEach(toggle);

function apply(focus) {
  const keep = focus ? new Set([focus, ...net.getConnectedNodes(focus)]) : null;
  nodes.update(nodes.get().map(n => ({id:n.id, hidden:hidden.has(n.folder), opacity:keep && !keep.has(n.id) ? 0.15 : 1})));
}
net.on("click", p => apply(p.nodes[0]));

const legend = document.getElementById("legend");
Object.entries(D.colors).forEach(([g, c]) => {
  const s = document.createElement("span"); s.className = "chip"; s.innerHTML = `<i style="background:${c}"></i>${g}`;
  s.onclick = () => { hidden.has(g) ? hidden.delete(g) : hidden.add(g); s.classList.toggle("off"); apply(); };
  legend.appendChild(s);
});
document.getElementById("q").addEventListener("keydown", e => {
  if (e.key !== "Enter") return;
  const q = e.target.value.trim().toLowerCase();
  const i = D.files.findIndex(f => f.label.toLowerCase().includes(q));
  if (i >= 0) { net.selectNodes([fid(i)]); net.focus(fid(i), {scale:1.4, animation:true}); apply(fid(i)); }
});
</script></body></html>
"""


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    src = sys.argv[1] if len(sys.argv) > 1 else "graphify-out/graph.json"
    out = sys.argv[2] if len(sys.argv) > 2 else "graphify-out/scripts.html"
    with open(src, encoding="utf-8") as f:
        data = build(json.load(f))
    with open(out, "w", encoding="utf-8") as f:
        f.write(HTML.replace("__DATA__", json.dumps(data, ensure_ascii=False, separators=(",", ":"))))
    methods = sum(1 for n in data["nodes"] if n[1])
    print(f"{out}: 스크립트 {len(data['files'])}개, 메서드 {methods}개, 원본 연결 {len(data['edges'])}개, 폴더 {len(data['colors'])}개")


if __name__ == "__main__":
    main()
