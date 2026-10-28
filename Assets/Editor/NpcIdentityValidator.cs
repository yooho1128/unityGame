#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Field;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class NpcIdentityValidator
    {
        public static void Validate(List<string> errors)
        {
            var entries = NpcIdentityCatalog.All;
            if (entries.Count != 65 || entries.Select(e => e.id).Distinct().Count() != entries.Count)
                errors.Add("NPC identity count/duplicate ID error");
            var regions = Story.RegionRepository.All.Where(r => r.isSettlement).ToList();
            foreach (var region in regions)
                foreach (var role in new[] { "healer", "rumor", "investigator", "craftsman", "merchant", "requester" })
                    if (NpcIdentityCatalog.Find(FieldNpcIdentity.ServiceId(region.regionId, role)) == null)
                        errors.Add($"Missing town NPC identity: {region.regionId}/{role}");
            var rects = new HashSet<string>();
            foreach (var e in entries)
            {
                var sprite = NpcIdentityCatalog.Portrait(e.id);
                if (string.IsNullOrEmpty(e.name) || string.IsNullOrEmpty(e.role) || sprite == null)
                { errors.Add($"Missing NPC name/role/art: {e.id}"); continue; }
                if (!rects.Add($"{e.sheet}/{sprite.rect}"))
                    errors.Add($"Reused NPC atlas cell: {e.id}");
                if (!sprite.texture.name.StartsWith("npc_atlas_"))
                    errors.Add($"NPC must use dedicated atlas: {e.id}");
            }
        }
    }
}
#endif
