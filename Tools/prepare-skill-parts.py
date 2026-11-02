"""Read generated atlases, describe 128 Unity sub-sprites, and dress every skill.

The PNGs are preserved byte for byte. Cropping below measures alpha bounds only;
Unity imports named sprite rectangles from the resulting manifest.
"""
from collections import Counter
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SHEETS = {
    "Weapons": """blade_crescent needle_streak claw_triple scissor_cross fang_upper fang_lower spear_wake arrow_volley
sickle_arc guillotine_drop blade_fan saw_arc gear_shatter twin_blade armor_shards impact_star
sonic_rings shock_wedge ground_crack stone_spires drill_cone glass_shards dash_ribbon meteor_streak
antler_strike barbed_chain broken_shackle banner_spear spark_spray contact_spark torn_fabric split_shockwave""".split(),
    "Elements": """fire_tongue fireball fire_pillar flame_plume flame_crown ember_cluster smoke_curl fire_explosion
ice_spear frost_snowflake crystal_cluster frozen_shell frost_mist ice_shards frost_vortex icicle_crown
wave_crest water_drop splash_crown whirlpool undertow water_ripples wind_funnel gust_ribbon
lightning_bolt electric_arc electric_orb electric_cage thorn_vines leaf_burst seed_blossom root_spiral""".split(),
    "Theatre": """moon_blade lunar_halo eclipse_corona moon_shards petal_spiral petal_single spectral_blossom veil_ribbon
taut_string severed_thread needle_thread cross_stitches string_web puppet_control thread_spool wire_cage
open_book torn_page rune_row arcane_seal ink_splash ink_stroke ink_vortex erasure_tear
weeping_mask smiling_mask mask_shards curtain_fold cracked_clock feather_curl bird_wing resonant_bell""".split(),
    "Sanctuary": """guardian_shield ward_crystal healing_ribbons healing_stars light_teardrop laurel_wreath cupped_hands lotus_base
sacred_beam halo_rays life_tree mending_heart healing_patch page_sanctuary hearth_flame resonance_curl
constellation galaxy_disc watching_eye audience_cluster grand_crown beast_head great_bird lunar_bride
puppet_hand proscenium cosmic_gate void_maw sun_crest mirror_fan final_sigil comet_burst""".split(),
}


def gutters(alpha, count, vertical):
    width, height = alpha.size
    length = width if vertical else height
    projection = []
    for n in range(length):
        strip = (n, 0, n + 1, height) if vertical else (0, n, width, n + 1)
        projection.append(sum(alpha.crop(strip).histogram()[24:]))
    boundaries = [0]
    for n in range(1, count):
        ideal = round(n * length / count)
        radius = round(length / count * .13)
        options = range(ideal - radius, ideal + radius + 1)
        boundaries.append(min(options, key=lambda x: (sum(projection[max(0, x-1):x+2]), abs(x-ideal))))
    return boundaries + [length]


def prepare_parts():
    parts = []
    for sheet, ids in SHEETS.items():
        assert len(ids) == 32
        path = ROOT / f"Assets/Resources/Art/SkillParts/Vfx{sheet}.png"
        image = Image.open(path)
        assert image.mode == "RGBA" and image.getextrema()[3][0] == 0
        alpha = image.getchannel("A")
        xs, ys = gutters(alpha, 8, True), gutters(alpha, 4, False)
        subjects = json.loads((ROOT / f"Docs/Generated/SkillParts128/prompt-{sheet.lower()}.json").read_text(encoding="utf-8-sig"))["subjects"]
        for index, part_id in enumerate(ids):
            col, row = index % 8, index // 8
            cell = (xs[col], ys[row], xs[col+1], ys[row+1])
            mask = alpha.crop(cell).point(lambda value: 255 if value >= 24 else 0)
            box = mask.getbbox()
            assert box and sum(mask.histogram()[128:]) > 100, part_id
            left = max(cell[0], cell[0] + box[0] - 3)
            top = max(cell[1], cell[1] + box[1] - 3)
            right = min(cell[2], cell[0] + box[2] + 3)
            bottom = min(cell[3], cell[1] + box[3] + 3)
            patch = image.crop((left, top, right, bottom))
            parts.append({
                "id": part_id, "sheet": f"Art/SkillParts/Vfx{sheet}", "category": sheet,
                "column": col, "row": row, "description": subjects[index],
                "rect": {"x": left, "y": image.height-bottom, "width": right-left, "height": bottom-top},
                "sha256": hashlib.sha256(patch.tobytes()).hexdigest(),
                "visiblePixels": sum(mask.histogram()[128:]),
            })
    assert len(parts) == len({part["id"] for part in parts}) == len({part["sha256"] for part in parts}) == 128
    path = ROOT / "Assets/Resources/Data/SkillPartCatalog.json"
    path.write_text(json.dumps({"version": 1, "parts": parts}, indent=2), encoding="utf-8")
    return parts


PALETTES = {
    "Slash": "split_shockwave blade_crescent impact_star spark_spray twin_blade",
    "Claw": "contact_spark claw_triple fang_upper armor_shards beast_head",
    "Thrust": "sonic_rings needle_streak shock_wedge glass_shards banner_spear",
    "Flame": "ember_cluster fireball fire_explosion fire_tongue flame_crown",
    "Frost": "frost_snowflake ice_spear frozen_shell ice_shards icicle_crown",
    "Moon": "lunar_halo moon_blade moon_shards petal_single eclipse_corona",
    "Thread": "string_web taut_string wire_cage severed_thread puppet_hand",
    "Feather": "feather_curl spear_wake bird_wing ember_cluster great_bird",
    "Petal": "petal_spiral veil_ribbon spectral_blossom petal_single lunar_bride",
    "Rune": "arcane_seal ink_stroke ink_splash torn_page open_book",
    "Wave": "water_ripples wave_crest splash_crown water_drop undertow",
    "Vine": "root_spiral thorn_vines seed_blossom leaf_burst life_tree",
    "Bolt": "electric_orb lightning_bolt electric_cage electric_arc comet_burst",
    "Rock": "ground_crack stone_spires armor_shards glass_shards ward_crystal",
    "Wind": "gust_ribbon wind_funnel drill_cone dash_ribbon bird_wing",
    "Mind": "watching_eye drill_cone mirror_fan crystal_cluster cosmic_gate",
    "Star": "constellation meteor_streak sun_crest healing_stars galaxy_disc",
    "Heal": "lotus_base healing_ribbons mending_heart light_teardrop guardian_shield",
}


def style(entry, skill):
    motif, formation, delivery = (entry[k] for k in ("motif", "formation", "delivery"))
    slots = dict(zip(("charge", "travel", "impact", "debris", "crest"), PALETTES[motif].split()))
    formations = {
        "Edge": {"impact": "impact_star"},
        "Jaw": {"impact": "fang_upper", "debris": "fang_lower"},
        "Crown": {"impact": "flame_crown" if motif == "Flame" else "grand_crown", "crest": "grand_crown"},
        "Prism": {"impact": "crystal_cluster", "debris": "ice_shards" if motif == "Frost" else "glass_shards"},
        "Sigil": {"impact": "arcane_seal", "debris": "rune_row"},
        "Rain": {"impact": "ice_shards" if motif == "Frost" else "arrow_volley"},
        "Tide": {"impact": "wave_crest", "charge": "water_ripples"},
        "Cage": {"impact": "wire_cage", "debris": "taut_string"},
        "Void": {"impact": "ink_vortex" if motif == "Rune" else "void_maw", "crest": "erasure_tear"},
        "Rays": {"impact": "electric_orb" if motif == "Bolt" else "sun_crest"},
        "Suture": {"impact": "cross_stitches", "debris": "needle_thread"},
        "Blossom": {"impact": "seed_blossom" if motif == "Vine" else "spectral_blossom"},
        "Stars": {"impact": "constellation", "crest": "galaxy_disc", "debris": "healing_stars"},
        "Shield": {"impact": "guardian_shield", "crest": "ward_crystal"},
        "Bell": {"impact": "resonant_bell", "debris": "resonance_curl"},
        "Wings": {"impact": "bird_wing", "crest": "great_bird", "debris": "feather_curl"},
        "Pages": {"impact": "open_book", "crest": "page_sanctuary", "debris": "torn_page"},
        "Clock": {"impact": "cracked_clock", "debris": "gear_shatter"},
        "Mask": {"impact": "weeping_mask", "crest": "smiling_mask", "debris": "mask_shards"},
        "Eclipse": {"impact": "eclipse_corona", "crest": "lunar_bride", "debris": "moon_shards"},
    }
    slots.update(formations[formation])
    if delivery == "Sweep":
        slots["travel"] = {"Flame": "sickle_arc", "Rune": "ink_stroke", "Thread": "severed_thread", "Feather": "feather_curl"}.get(motif, "blade_crescent")
    elif delivery == "Cross":
        slots["travel"] = "claw_triple" if motif == "Claw" else "severed_thread" if motif == "Thread" else "scissor_cross"
    elif delivery == "Volley":
        slots["travel"] = "ice_spear" if motif == "Frost" else "torn_page" if motif == "Rune" else "arrow_volley"
    elif delivery == "Breath":
        slots["travel"] = {"Frost": "frost_mist", "Flame": "flame_plume", "Rune": "ink_stroke"}.get(motif, "gust_ribbon")
    elif delivery == "Descend":
        slots["travel"] = {"Frost": "ice_spear", "Flame": "fire_pillar", "Slash": "guillotine_drop", "Rune": "erasure_tear"}.get(motif, "meteor_streak")
    elif delivery == "Rush":
        slots["travel"] = "dash_ribbon" if motif not in ("Claw", "Moon", "Flame") else slots["travel"]
    elif delivery == "Ripple":
        slots["travel"] = "water_ripples" if motif == "Wave" else "sonic_rings"
    elif delivery == "Tether":
        slots["travel"] = "barbed_chain" if motif == "Rock" else "taut_string"
    elif delivery == "Gate":
        slots["travel"] = "curtain_fold" if motif in ("Rune", "Thread") else "cosmic_gate"
    elif delivery == "Column":
        slots["travel"] = {"Flame": "fire_pillar", "Bolt": "lightning_bolt"}.get(motif, "sacred_beam")
    elif delivery == "Fan":
        slots["travel"] = "blade_fan" if motif == "Slash" else slots["travel"]
    elif delivery == "Siphon":
        slots["travel"] = "ink_vortex" if motif == "Rune" else slots["travel"]
    if skill.get("target") == "Self" and skill.get("healRatio", 0) > 0:
        slots.update(charge="lotus_base", travel="healing_ribbons")
        if delivery == "Weave":
            slots.update(impact="healing_patch", debris="needle_thread")
        elif delivery == "Bloom":
            slots.update(impact="life_tree" if motif == "Vine" else "hearth_flame" if motif == "Flame" else "spectral_blossom")
        elif formation == "Eclipse":
            slots.update(impact="lunar_halo", debris="light_teardrop", crest="halo_rays")
        elif formation == "Pages":
            slots.update(impact="page_sanctuary", debris="torn_page", crest="open_book")
        elif formation == "Mask":
            slots.update(impact="smiling_mask", debris="light_teardrop", crest="cupped_hands")
        elif formation == "Stars":
            slots.update(impact="cupped_hands", debris="healing_stars", crest="galaxy_disc")
        elif formation == "Bell":
            slots.update(impact="mending_heart", debris="resonance_curl", crest="halo_rays")
        elif formation == "Tide":
            slots.update(impact="lotus_base", debris="water_drop", crest="halo_rays")
        elif formation == "Shield":
            slots.update(impact="guardian_shield", debris="laurel_wreath", crest="ward_crystal")
    return slots


# Named directions take precedence over motif defaults. These are semantic assignments,
# not random choices or palette swaps used to inflate the number of parts.
OVERRIDES = {
    "slash": "travel=blade_crescent impact=contact_spark debris=spark_spray",
    "inkshot": "travel=ink_stroke impact=ink_splash debris=rune_row",
    "claw": "travel=claw_triple impact=fang_upper debris=fang_lower",
    "peck": "travel=needle_streak impact=shock_wedge debris=contact_spark",
    "ember_strike": "charge=ember_cluster travel=sickle_arc impact=fire_explosion debris=fire_tongue",
    "fallen_crown": "charge=flame_crown travel=fire_pillar impact=fire_explosion debris=ember_cluster crest=grand_crown",
    "guardian_oath": "charge=laurel_wreath impact=guardian_shield debris=healing_stars crest=ward_crystal",
    "ash_execution": "travel=guillotine_drop impact=split_shockwave debris=ember_cluster crest=flame_crown",
    "frost_sigil": "charge=frost_snowflake impact=arcane_seal debris=ice_shards",
    "forbidden_reading": "travel=torn_page impact=ink_vortex debris=rune_row crest=open_book",
    "memory_sanctuary": "charge=arcane_seal impact=page_sanctuary debris=torn_page crest=open_book",
    "zero_prophecy": "charge=frost_vortex travel=ice_spear impact=frozen_shell debris=ice_shards crest=icicle_crown",
    "moon_howl": "charge=lunar_halo travel=sonic_rings impact=resonant_bell debris=moon_shards",
    "homeward_howl": "impact=lunar_halo debris=healing_stars crest=beast_head",
    "blood_fang": "travel=claw_triple impact=fang_upper debris=fang_lower crest=beast_head",
    "cut_string": "charge=taut_string travel=scissor_cross impact=cross_stitches debris=severed_thread",
    "stitch": "charge=needle_thread travel=healing_ribbons impact=healing_patch debris=cross_stitches crest=mending_heart",
    "puppet_free_dance": "charge=puppet_control impact=severed_thread debris=veil_ribbon crest=proscenium",
    "puppet_string_reversal": "charge=puppet_control travel=taut_string impact=wire_cage debris=severed_thread crest=puppet_hand",
    "crow_dawn_beacon": "charge=halo_rays travel=sacred_beam impact=sun_crest debris=feather_curl crest=great_bird",
    "crow_sky_cremation": "charge=fire_tongue travel=meteor_streak impact=fire_explosion debris=ember_cluster crest=great_bird",
    "mask_true_smile": "charge=light_teardrop impact=smiling_mask debris=light_teardrop crest=cupped_hands",
    "mask_frozen_ovation": "charge=weeping_mask impact=mask_shards debris=ice_shards crest=weeping_mask",
    "black_ink": "travel=ink_stroke impact=ink_splash debris=ink_vortex",
    "erasure": "charge=arcane_seal travel=erasure_tear impact=erasure_tear debris=rune_row crest=final_sigil",
    "last_standard": "travel=banner_spear impact=split_shockwave debris=torn_fabric crest=banner_spear",
    "coal_burst": "travel=fireball impact=fire_explosion debris=ember_cluster",
    "crown_inferno": "travel=flame_plume impact=fire_explosion debris=ember_cluster crest=grand_crown",
    "hearth_mend": "charge=lotus_base impact=hearth_flame debris=ember_cluster crest=cupped_hands",
    "soot_arrow": "travel=arrow_volley impact=contact_spark debris=smoke_curl",
    "smoke_mark": "travel=smoke_curl impact=ink_splash debris=smoke_curl",
    "headless_cleave": "travel=guillotine_drop impact=shock_wedge debris=armor_shards",
    "loyal_guard": "impact=ward_crystal debris=armor_shards",
    "icewing_cut": "travel=feather_curl impact=blade_fan debris=ice_shards",
    "prism_snow": "charge=frost_snowflake travel=ice_spear impact=mirror_fan debris=crystal_cluster",
    "zero_page_blizzard": "travel=frost_mist impact=frost_vortex debris=torn_page crest=icicle_crown",
    "antler_crash": "travel=antler_strike impact=antler_strike debris=leaf_burst",
    "forest_memory": "charge=root_spiral impact=life_tree debris=laurel_wreath crest=seed_blossom",
    "sonic_fang": "travel=saw_arc impact=sonic_rings debris=fang_lower",
    "heart_pulse": "charge=halo_rays impact=mending_heart debris=light_teardrop",
    "gear_bite": "travel=gear_shatter impact=gear_shatter debris=glass_shards",
    "second_hand_kick": "travel=twin_blade impact=cracked_clock debris=gear_shatter",
    "scissor_tempo": "travel=scissor_cross impact=blade_fan debris=severed_thread",
    "encore_mend": "charge=resonance_curl travel=healing_ribbons impact=resonant_bell debris=healing_stars",
    "patchwork_guard": "impact=healing_patch debris=torn_fabric crest=guardian_shield",
    "thousand_strings": "charge=thread_spool travel=taut_string impact=string_web debris=needle_thread crest=puppet_hand",
    "free_stage_finale": "charge=curtain_fold travel=severed_thread impact=bird_wing debris=torn_fabric crest=proscenium",
    "shackle_crush": "charge=barbed_chain travel=barbed_chain impact=broken_shackle debris=armor_shards",
    "final_censorship": "travel=erasure_tear impact=ink_vortex debris=mask_shards crest=final_sigil",
    "passenger_last_arrival": "charge=halo_rays impact=audience_cluster debris=healing_stars crest=proscenium",
    "glass_tide": "travel=undertow impact=splash_crown debris=glass_shards",
    "pigment_undertow": "travel=undertow impact=whirlpool debris=ink_splash",
    "storm_beacon": "charge=electric_orb travel=lightning_bolt impact=electric_cage debris=electric_arc",
    "glass_gaze": "charge=watching_eye impact=watching_eye debris=glass_shards",
    "moonsea_crown": "travel=wave_crest impact=splash_crown debris=moon_shards crest=grand_crown",
    "true_name_selene": "charge=lunar_halo impact=whirlpool debris=water_drop crest=lunar_bride",
    "director_threads": "charge=puppet_control travel=taut_string impact=puppet_hand debris=string_web",
    "outside_the_script": "charge=proscenium travel=cosmic_gate impact=erasure_tear debris=torn_page crest=cosmic_gate",
    "eclipse_widow": "charge=lunar_halo impact=eclipse_corona debris=petal_spiral crest=lunar_bride",
    "curtain_call_zero": "charge=curtain_fold travel=proscenium impact=final_sigil debris=rune_row crest=proscenium",
    "marionette_world": "charge=puppet_control impact=string_web debris=torn_page crest=puppet_hand",
    "empty_applause": "charge=resonance_curl impact=audience_cluster debris=healing_stars",
    "cosmic_final_ovation": "charge=constellation travel=comet_burst impact=galaxy_disc debris=healing_stars crest=audience_cluster",
    "we_will_remember": "charge=halo_rays impact=audience_cluster debris=healing_stars crest=cupped_hands",
    "devouring_auditorium": "charge=watching_eye travel=galaxy_disc impact=void_maw debris=meteor_streak crest=proscenium",
    "ele_spark_spool_squirrel_motif": "charge=electric_orb travel=electric_arc impact=thread_spool debris=lightning_bolt",
    "ele_spark_spool_squirrel_restored": "travel=needle_thread impact=electric_cage debris=cross_stitches",
    "ele_clay_bell_tortoise_touch": "travel=stone_spires impact=ground_crack debris=armor_shards",
    "ele_clay_bell_tortoise_restored": "travel=stone_spires impact=ground_crack debris=glass_shards crest=resonant_bell",
    "ele_dream_marble_rabbit_touch": "travel=electric_orb impact=drill_cone debris=healing_stars",
    "ele_dream_marble_rabbit_restored": "travel=cosmic_gate impact=mirror_fan debris=crystal_cluster",
}


# Regional signature skills and tier-growth ultimates are created by the Unity
# content generators rather than the JSON skill arrays. Keep their direction
# here too, so a fresh checkout gets the same coverage as an existing project.
GENERATED_DIRECTIONS = """
bloodmane_stalker_salvation_ultimate Claw Rush Jaw Embers Pounce 4 -15
bloodmane_stalker_grudge_ultimate Flame Breath Jaw Collapse Pounce 5 20
book_drake_salvation_ultimate Rune Volley Pages Ascend Hover 4 -10
book_drake_grudge_ultimate Frost Descend Jaw Shatter Hover 5 15
drowned_painter_salvation_ultimate Wave Arc Prism Return Dance 3 -20
drowned_painter_grudge_ultimate Rune Siphon Void Collapse Recoil 4 25
extract_subject_m_salvation_ultimate Flame Rush Rays Ascend Charge 4 -10
extract_subject_m_grudge_ultimate Flame Descend Crown Embers Charge 5 15
headless_guard_salvation_ultimate Slash Thrust Shield Return Charge 3 0
headless_guard_grudge_ultimate Slash Descend Edge Scatter Cut 5 -30
interlude_manager_salvation_ultimate Rune Gate Clock Return Still 4 0
interlude_manager_grudge_ultimate Thread Tether Clock Collapse Raise 5 20
inverted_librarian_salvation_ultimate Frost Orbit Pages Ascend Hover 4 -15
inverted_librarian_grudge_ultimate Rune Descend Void Collapse Raise 5 25
kneeling_captain_salvation_ultimate Flame Lift Crown Ascend Raise 3 0
kneeling_captain_grudge_ultimate Slash Cross Crown Embers Cut 5 -25
mourning_keeper_salvation_ultimate Star Column Bell Ascend Raise 3 0
mourning_keeper_grudge_ultimate Moon Ripple Eclipse Collapse Hover 5 20
outside_script_shadow_salvation_ultimate Star Gate Stars Ascend Still 4 -10
outside_script_shadow_grudge_ultimate Rune Gate Void Collapse Recoil 5 15
rival_director_salvation_ultimate Thread Weave Cage Return Dance 4 -20
rival_director_grudge_ultimate Thread Tether Mask Collapse Raise 5 25
wire_primadonna_salvation_ultimate Thread Fan Wings Ascend Dance 4 -15
wire_primadonna_grudge_ultimate Thread Cross Suture Scatter Dance 5 30
zero_warden_salvation_ultimate Frost Lift Shield Ascend Still 3 0
zero_warden_grudge_ultimate Frost Descend Cage Shatter Charge 5 -20
exp_black_archive_01_signature Rune Thrust Edge Scatter Cut 3 -25
exp_black_archive_02_signature Rune Volley Pages Fade Hover 4 15
exp_bloodmoon_ridge_01_signature Moon Ripple Bell Scatter Raise 3 0
exp_blue_abyss_01_signature Frost Thrust Prism Shatter Charge 3 -10
exp_blue_abyss_02_signature Frost Seal Sigil Fade Still 2 15
exp_clockwork_heart_stage_01_signature Thread Orbit Clock Return Dance 4 20
exp_echo_village_01_signature Rune Sweep Edge Fade Cut 2 -20
exp_echo_village_02_signature Mind Orbit Stars Return Hover 3 15
exp_echo_village_03_signature Wind Volley Wings Scatter Hover 4 -10
exp_forbidden_stacks_01_signature Rune Breath Pages Scatter Hover 3 10
exp_forgotten_tent_01_signature Moon Fan Mask Fade Dance 2 -15
exp_ghost_orchestra_pit_01_signature Star Ripple Bell Return Raise 3 0
exp_matte_projection_room_01_signature Rune Column Void Collapse Still 4 0
exp_moon_eater_ritual_01_signature Moon Descend Eclipse Collapse Pounce 4 20
exp_moonfang_forest_01_signature Claw Cross Jaw Scatter Pounce 3 -25
exp_moonlit_boss_stage_01_signature Thread Tether Suture Return Raise 3 10
exp_moonlit_boss_stage_02_signature Rune Seal Pages Fade Hover 2 -10
exp_mourning_lighthouse_01_signature Wave Arc Rays Ascend Still 3 15
exp_name_islands_01_signature Wave Ripple Bell Return Raise 4 0
exp_painted_wagon_road_01_signature Thread Sweep Suture Knit Cut 3 -20
exp_poster_alley_01_signature Flame Sweep Edge Embers Cut 3 -30
exp_poster_alley_02_signature Flame Volley Pages Embers Hover 4 15
exp_puppeteer_stage_01_signature Thread Ripple Clock Return Dance 3 20
exp_puppeteer_stage_02_signature Thread Tether Cage Collapse Raise 4 -15
exp_sealed_small_theater_01_signature Flame Fan Mask Embers Dance 4 20
exp_severed_workshop_01_signature Thread Thrust Suture Scatter Cut 2 -10
exp_severed_workshop_02_signature Thread Weave Stars Knit Raise 4 15
exp_silent_clown_stage_01_signature Mind Seal Mask Fade Still 3 0
exp_silent_court_01_signature Rune Thrust Sigil Scatter Charge 3 -15
exp_silent_court_02_signature Rune Volley Cage Collapse Still 3 20
exp_sleeping_beast_den_01_signature Moon Breath Jaw Scatter Pounce 4 -20
exp_stitch_field_01_signature Thread Thrust Shield Knit Charge 3 10
exp_violet_marsh_01_signature Claw Rush Jaw Scatter Pounce 3 -15
exp_widow_moon_palace_01_signature Wave Orbit Eclipse Return Dance 3 20
exp_widow_moon_palace_02_signature Wave Arc Blossom Ascend Dance 4 -15
exp_widow_moon_palace_03_signature Wave Fan Mask Scatter Dance 3 25
"""


def dress_skills(parts):
    known = {}
    for path in (ROOT / "Assets/Resources/Data").glob("*Catalog.json"):
        for skill in json.loads(path.read_text(encoding="utf-8-sig")).get("skills", []):
            known[skill["skillId"]] = skill
    path = ROOT / "Assets/Resources/Data/SkillPresentationCatalog.json"
    catalog = json.loads(path.read_text(encoding="utf-8-sig"))
    generated = {}
    for line in GENERATED_DIRECTIONS.splitlines():
        if not line.strip():
            continue
        skill_id, motif, delivery, formation, finish, motion, beats, turn = line.split()
        generated[skill_id] = dict(skillId=skill_id, motif=motif, delivery=delivery,
                                   formation=formation, finish=finish, motion=motion,
                                   beats=int(beats), turn=float(turn))
    existing = {entry["skillId"] for entry in catalog["entries"]}
    catalog["entries"].extend(generated[key] for key in sorted(generated.keys() - existing))
    ids = {part["id"] for part in parts}
    usage = Counter()
    for entry in catalog["entries"]:
        assert entry["skillId"] in known or entry["skillId"] in generated
        slots = style(entry, known.get(entry["skillId"], {}))
        for assignment in OVERRIDES.get(entry["skillId"], "").split():
            slot, part = assignment.split("=")
            slots[slot] = part
        assert set(slots.values()) <= ids
        entry.update(slots)
        usage.update(slots.values())
    unused = ids - usage.keys()
    assert not unused, f"Art not assigned to a skill: {sorted(unused)}"
    path.write_text(json.dumps(catalog, indent=2), encoding="utf-8")
    report = {"newParts": len(parts), "dressedSkills": len(catalog["entries"]), "references": dict(sorted(usage.items()))}
    (ROOT / "Docs/Generated/SkillParts128/assignments.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"parts": len(parts), "skills": len(catalog["entries"]), "minReferences": min(usage.values())}))


if __name__ == "__main__":
    dress_skills(prepare_parts())
