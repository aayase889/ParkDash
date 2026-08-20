using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ColorSortLevelDatabase", menuName = "Color Sort/Level Database")]
public class ColorSortLevelDatabase : ScriptableObject
{
    public List<UnityGameManager.LevelConfig> levels = new List<UnityGameManager.LevelConfig>();
}
