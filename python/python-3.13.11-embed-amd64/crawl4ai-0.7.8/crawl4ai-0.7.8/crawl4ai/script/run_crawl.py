#!/usr/bin/env python3
import sys
import json
import traceback

# 예제: stdin으로 {"url":"..."} 같은 입력을 받고 콘텐츠를 크롤링(또는 crawl4ai 호출)해 {"success": true, "content": "..."} 를 출력
def main():
    try:
        raw = sys.stdin.read()
        if not raw:
            # 또는 CLI 인수로 받을 수 있도록 처리
            if len(sys.argv) > 1:
                raw = json.dumps({"arg": sys.argv[1]})
            else:
                raw = "{}"

        inp = json.loads(raw)

        url = inp.get("url")
        # TODO: crawl4ai 호출 또는 내부 크롤 로직 수행
        # 예: content = crawl4ai_get_text(url)
        # 아래는 fallback - 단순 요청(실사용에선 requests, 예외처리, 타임아웃 등 필요)
        content = crawl4ai_get_text(url)

        result = {
            "success": True,
            "url": url,
            "content": content
        }

        # 반드시 stdout에는 JSON만 찍는다
        print(json.dumps(result, ensure_ascii=False))
    except Exception as ex:
        # 에러는 stderr로 내보내고, stdout에는 반드시 JSON 에러 구조를 출력하도록 약속해도 좋다
        err = {"success": False, "error": str(ex)}
        try:
            print(json.dumps(err, ensure_ascii=False))
        except:
            print(json.dumps({"success": False, "error": "unknown"}))
        # 상세 로그는 stderr
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()