using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Skin", fileName = "Skin")]
    public class SkinAsset : DefAsset<SkinDef>
    {
        public GameObject prefab;
        public Sprite thumbnail;
    }
}
