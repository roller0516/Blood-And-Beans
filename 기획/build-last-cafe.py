# last-cafe.template.html -> last-cafe-prototype.html
# 페이지가 자기 자신을 다시 publish 할 수 있도록, 완전한 문서 형태의 템플릿을
# base64로 박아 넣는다. 템플릿 안의 플레이스홀더는 인코딩된 쪽에 그대로 남아
# 다음 세대도 같은 방식으로 자기복제한다.
import base64, json, pathlib, re

here = pathlib.Path(__file__).parent
tpl = (here / "last-cafe.template.html").read_text(encoding="utf-8")

title = re.search(r"<title>.*?</title>", tpl, re.S).group(0)
body = tpl.replace(title, "", 1).lstrip()

full = (
    "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\">"
    "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
    + title +
    "</head><body>\n" + body + "\n</body></html>"
)

src_b64 = base64.b64encode(full.encode("utf-8")).decode("ascii")

INITIAL = {"v": 1, "players": [{
    "id": "bot", "name": "세 번째 골목", "bot": True, "spot": 1,
    "sales": 0, "day": 1, "owned": [3], "menu": [3, -1, -1], "found": [], "log": []
}]}

# 각 플레이스홀더의 최초 1회만 치환한다. 뒤쪽 등장분은 페이지가 스스로를 다시
# 만들 때 쓰는 치환 코드의 리터럴이라 그대로 남아 있어야 한다.
out = tpl.replace("__SRC_B64__", src_b64, 1).replace(
    "__STATE__", json.dumps(INITIAL, ensure_ascii=False).replace("</", "<\\/"), 1)
assert tpl.index("__SRC_B64__") < tpl.index("const SRC_B64") + 40, "SRC_B64 주입 지점이 최초 등장이어야 함"
assert tpl.index("__STATE__") < tpl.index("</script>"), "STATE 주입 지점이 최초 등장이어야 함"
assert out.count("__SRC_B64__") >= 1 and out.count("__STATE__") >= 1, "자기복제용 리터럴이 남아 있어야 함"
assert src_b64 in out and "__STATE__" not in out.split("</script>")[0], "주입 실패"

dst = here / "last-cafe-prototype.html"
dst.write_text(out, encoding="utf-8")
print("wrote", dst.name, len(out), "chars / embedded template", len(src_b64), "b64 chars")
