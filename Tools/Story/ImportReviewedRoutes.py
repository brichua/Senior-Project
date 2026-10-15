"""Import the reviewed Markdown into existing Unity story assets without changing save IDs.

Run with Python 3 from any directory; pass the master Markdown path explicitly.
Only the six character stories and their starting-deck assets are written.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid

PROJECT = Path(__file__).resolve().parents[2]
NAMES = ["Miku", "Len", "Rin", "KAITO", "MEIKO", "Luka"]
ASSETS = ["HatsuneMiku", "KagamineLen", "KagamineRin", "KAITO", "MEIKO", "MegurineLuka"]
CLASS_FILES = ["miku", "len", "rin", "kaito", "meiko", "luka"]
BASE_DECKS = ["starter", "len ai", "rin audition", "kaito audition", "meiko audition", "luka audition"]
COUNTS = [74, 73, 74, 73, 72, 73]


def read(path):
    return path.read_text(encoding="utf-8-sig")


def field(text, name):
    return re.search(r"^  " + re.escape(name) + r": (.+)$", text, re.M)[1]


def guid(path):
    return re.search(r"^guid: (\w+)$", read(Path(str(path) + ".meta")), re.M)[1]


def ref(path):
    return "{fileID: 11400000, guid: " + guid(path) + ", type: 2}"


def quote(value):
    return json.dumps(value, ensure_ascii=False)


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def meta(path):
    target = Path(str(path) + ".meta")
    if not target.exists():
        write(target, "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex +
              "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def parse(master):
    routes = {}
    for match in re.finditer(r"^# (Miku|Rin|Len|Luka|KAITO|MEIKO) Route\n(.*?)(?=^# |\Z)", master, re.M | re.S):
        name, body = match.groups()
        scenes = {}
        for scene in re.finditer(r"^## (\d+)\. ([^\n]+)\n(.*?)(?=^## |\Z)", body, re.M | re.S):
            number, title, content = scene.groups()
            location = re.search(r"^\*Location: (.+)\*$", content, re.M)
            lines = [{"speaker": speaker, "text": re.sub(r"\*([^*]+)\*", r"\1", text)}
                     for speaker, text in re.findall(r"^\*\*([^*]+):\*\* (.+)$", content, re.M)]
            assert all(l["speaker"] in NAMES + ["Narrator"] for l in lines), title
            assert 4 <= len(lines) <= (8 if title == "Ending" else 6), title
            assert title not in scenes
            scenes[title] = {"location": location[1] if location else "", "lines": lines}
        assert len(scenes) == 12, name
        index = NAMES.index(name)
        assert sum(len(s["lines"]) for s in scenes.values()) == COUNTS[index], name
        auditions = []
        for opponent in NAMES:
            if opponent == name:
                continue
            before = scenes[opponent + " — Before Battle"]
            after = scenes[opponent + " — After Battle"]
            assert before["location"], opponent
            auditions.append({"opponent": NAMES.index(opponent), "location": before["location"],
                              "before": {"lines": before["lines"]}, "after": {"lines": after["lines"]}})
        routes[name] = {"assetName": ASSETS[index], "characterClass": index,
                        "opening": {"lines": scenes["Opening"]["lines"]},
                        "ending": {"lines": scenes["Ending"]["lines"]}, "auditions": auditions}
    assert len(routes) == 6
    return [routes[name] for name in NAMES]


def dialogue(script, route, portraits, art, indent, opponent=None, ending=False):
    present = {route} | {NAMES.index(l["speaker"]) for l in script["lines"] if l["speaker"] != "Narrator"}
    if opponent is not None:
        present.add(opponent)
    if ending and route == 0:
        present = set(range(6))
    # Put the protagonist on the left and the rival on the right; other cast fill middle slots.
    placement = {route: 0}
    if opponent is not None:
        placement[opponent] = 5
    remaining = sorted(present - set(placement))
    free_slots = [s for s in range(1, 6) if s not in placement.values()]
    placement.update(zip(remaining, free_slots))
    result = [" " * indent + "lines:"]
    for line in script["lines"]:
        speaker = line["speaker"]
        portrait = "{fileID: 0}" if speaker == "Narrator" else portraits[NAMES.index(speaker)]
        result += [" " * indent + "- speaker: " + quote(speaker),
                   " " * (indent + 2) + "portrait: " + portrait,
                   " " * (indent + 2) + "text: " + quote(line["text"]),
                   " " * (indent + 2) + "avatars:"]
        for identity, slot in sorted(placement.items(), key=lambda entry: entry[1]):
            result += [" " * (indent + 2) + "- slot: " + str(slot),
                       " " * (indent + 4) + "sprite: " + art[identity]]
    return "\n".join(result) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("master", type=Path)
    args = parser.parse_args()
    source_bytes = args.master.read_bytes()
    routes = parse(read(args.master))
    story_dir, deck_dir = PROJECT / "Assets/Story", PROJECT / "Assets/Decks"
    portraits, art = [], []
    for name in CLASS_FILES:
        content = read(PROJECT / "Assets/Classes" / (name + ".asset"))
        portraits.append(field(content, "classIcon"))
        art.append(field(content, "storyArt"))
    old_miku = read(story_dir / "HatsuneMiku.asset")
    rewards = {int(opponent): reward for opponent, reward in
               re.findall(r"  - opponent: (\d+)\n.*?    firstClearReward: ([^\n]+)", old_miku, re.S)}
    rewards[0] = ref(PROJECT / "Assets/Card Obj/Miku/world is mine.asset")
    enemy_decks = [ref(deck_dir / (name + ".asset")) for name in BASE_DECKS]
    descriptions = ["An unfinished song, ready for an audience.", "An original melody and a familiar rival.",
                    "A lively challenge from Rin.", "A full song beyond a famous chorus.",
                    "A complete live performance.", "Different words for an unsigned melody."]
    for route in routes:
        index = route["characterClass"]
        player_deck = deck_dir / (CLASS_FILES[index] + " story starter.asset")
        base = read(deck_dir / (BASE_DECKS[index] + ".asset"))
        base = re.sub(r"^  m_Name:.*$", "  m_Name: " + CLASS_FILES[index] + " story starter", base, flags=re.M)
        base = re.sub(r"^  deckName:.*$", "  deckName: " + NAMES[index] + " Story", base, flags=re.M)
        write(player_deck, base)
        meta(player_deck)
        path = story_dir / (route["assetName"] + ".asset")
        old = read(path)
        prefix = old.split("  prologue:", 1)[0]
        header = "World Tour Auditions — " + NAMES[index]
        prefix = re.sub(r"^  header:.*$", "  header: " + quote(header), prefix, flags=re.M)
        prefix = re.sub(r"^  playerStarterDeck:.*\n", "", prefix, flags=re.M)
        text = prefix + "  playerStarterDeck: " + ref(player_deck) + "\n  prologue:\n"
        text += dialogue(route["opening"], index, portraits, art, 4)
        text += "  epilogue:\n" + dialogue(route["ending"], index, portraits, art, 4, ending=True)
        text += "  auditions:\n"
        for audition in route["auditions"]:
            opponent = audition["opponent"]
            text += "  - opponent: " + str(opponent) + "\n"
            text += "    deckDescription: " + quote(descriptions[opponent]) + "\n"
            text += "    location: " + quote(audition["location"]) + "\n"
            text += "    auditionText: " + quote("An audition against " + NAMES[opponent] + ".") + "\n"
            text += "    firstClearReward: " + rewards[opponent] + "\n"
            for difficulty in ["easy", "medium", "hard"]:
                text += "    " + difficulty + "Deck: " + enemy_decks[opponent] + "\n"
            text += "    before:\n" + dialogue(audition["before"], index, portraits, art, 6, opponent)
            text += "    after:\n" + dialogue(audition["after"], index, portraits, art, 6, opponent)
        write(path, text)
    manifest = {"sourceSha256": hashlib.sha256(source_bytes).hexdigest(), "routes": routes}
    write(PROJECT / "Tools/Story/ReviewedRoutes.json", json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print("Imported six routes: 72 sequences, " + str(sum(COUNTS)) + " boxes. Existing story IDs and .meta files preserved.")


if __name__ == "__main__":
    main()
