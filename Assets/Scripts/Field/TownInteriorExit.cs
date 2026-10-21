using UnityEngine;

namespace ShadowTheater.Field
{
    public sealed class TownInteriorExit:MonoBehaviour,IInteractable
    {
        private TownInteriorController _owner;
        public void Configure(TownInteriorController owner)=>_owner=owner;
        public void Interact(PlayerController player)=>_owner?.Exit();
    }
}
