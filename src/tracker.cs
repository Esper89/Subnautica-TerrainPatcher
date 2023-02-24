using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class tracker : MonoBehaviour
    {
        public void OnDisable()
        {
            transform.position += twoloop.OriginShift.LocalOffset.ToVector3();
        }
    }
}