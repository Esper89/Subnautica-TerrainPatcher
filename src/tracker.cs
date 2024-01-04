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

        public void GetReadyForSave()
        {
            ErrorMessage.AddMessage("Bracing for save!");
            transform.position += twoloop.OriginShift.LocalOffset.ToVector3();
        }

        public void StopSave()
        {
            ErrorMessage.AddMessage("Saved Properly!");
            transform.position -= twoloop.OriginShift.LocalOffset.ToVector3();
        }
    }
}