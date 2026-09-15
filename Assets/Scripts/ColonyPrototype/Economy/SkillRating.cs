using System;
using UnityEngine;

namespace AsteroidColony
{
    [Serializable]
    public class SkillRating
    {
        public SkillDefinition skill;
        [Range(0f, 1f)]
        public float proficiency;

        public void Clamp()
        {
            proficiency = Mathf.Clamp01(proficiency);
        }
    }
}
