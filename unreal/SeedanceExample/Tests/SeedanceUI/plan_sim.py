"""测试辅助：把 planner 的 ops 应用到内存里的控件树，再导出成 planner 吃的 {name: {"class", "parent", "index"}}。"""

CANVAS = "/Script/UMG.CanvasPanel"
ROOT_CHAIN = [("Root", CANVAS, None), ("ScaleBox", "/Script/UMG.ScaleBox", "Root"),
              ("SizeBox", "/Script/UMG.SizeBox", "ScaleBox"), ("Design", CANVAS, "SizeBox")]


class Tree:
    def __init__(self):
        self.children = {}  # parent（根为 None）→ [name]
        self.cls = {}
        self.parent = {}
        self.containers = set()  # 导入建的容器（用于判断容器第一个子节点是不是显示控件）

    def add(self, name, cls, parent, index=-1):
        assert name not in self.cls, name
        self.cls[name] = cls
        self.parent[name] = parent
        kids = self.children.setdefault(parent, [])
        kids.insert(len(kids) if index < 0 else index, name)

    def detach(self, name):
        self.children[self.parent[name]].remove(name)

    def remove(self, name):
        for c in list(self.children.get(name, [])):
            self.remove(c)
        self.detach(name)
        self.children.pop(name, None)
        self.containers.discard(name)
        del self.cls[name], self.parent[name]

    def move(self, name, parent, index):
        self.detach(name)
        self.parent[name] = parent
        kids = self.children.setdefault(parent, [])
        kids.insert(len(kids) if index < 0 else index, name)

    def rename(self, old, new):
        assert new not in self.cls
        self.cls[new] = self.cls.pop(old)
        p = self.parent.pop(old)
        self.parent[new] = p
        kids = self.children[p]
        kids[kids.index(old)] = new
        self.children[new] = self.children.pop(old, [])
        for c in self.children[new]:
            self.parent[c] = new
        if old in self.containers:
            self.containers.discard(old)
            self.containers.add(new)

    def insert_index(self, parent, after):
        kids = self.children.setdefault(parent, [])
        if after is None:
            return 1 if kids and kids[0] not in self.containers else 0
        return kids.index(after) + 1

    def as_dict(self):
        out = {}
        for p, kids in self.children.items():
            for i, n in enumerate(kids):
                out[n] = {"class": self.cls[n], "parent": p, "index": i}
        return out

    def apply(self, ops):
        for o in ops:
            k = o["op"]
            if k == "ensure_root":
                for n, c, p in ROOT_CHAIN:
                    if n not in self.cls:
                        self.add(n, c, p)
            elif k == "add_container":
                idx = self.insert_index(o["parent"], o["after"]) if "after" in o else -1
                self.add(o["name"], CANVAS, o["parent"], idx)
                self.containers.add(o["name"])
            elif k == "add_view":
                self.add(o["name"], o["cls"], o["container"], 0)
            elif k == "move":
                self.detach(o["name"])
                self.parent[o["name"]] = o["parent"]
                self.children.setdefault(o["parent"], []).insert(self.insert_index(o["parent"], o["after"]), o["name"])
        return self


def tree_from_plan(p, tree=None):
    return (tree or Tree()).apply(p["ops"])
