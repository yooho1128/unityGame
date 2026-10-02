using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Story
{
    [Serializable]
    public class RegionCatalogData
    {
        public List<RegionData> regions = new List<RegionData>();
    }

    /// <summary>월드맵 한 지역의 진행 순서, 연결 경로, 출현 그림자와 씬 정보를 정의한다.</summary>
    [Serializable]
    public class RegionData
    {
        public string regionId;
        public string displayName;
        public string sceneName;
        public int order;
        public int act;
        public string actTitle;
        public string environment;
        public string summary;
        public int recommendedLevelMin;
        public int recommendedLevelMax;
        public bool isSettlement;
        public bool isDungeon;
        public bool isLegendary;
        public string requiredFlag;
        public string accentHex = "7650C8";
        public int arrivalX;
        public int arrivalY;
        public int arrivalFacing;
        public string bossShadowName;
        public string[] featuredShadows = Array.Empty<string>();
        public string[] connectedRegionIds = Array.Empty<string>();

        public Vector2Int ArrivalCell => new Vector2Int(arrivalX, arrivalY);

        public Color AccentColor
        {
            get
            {
                return ColorUtility.TryParseHtmlString("#" + accentHex, out var color)
                    ? color : new Color(.46f, .31f, .78f, 1f);
            }
        }
    }
}
