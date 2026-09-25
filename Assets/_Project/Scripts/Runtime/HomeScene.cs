using Squishy.Data;
using Squishy.Runtime.CameraRig;
using Squishy.Runtime.Rendering;
using Squishy.Runtime.Room;
using Squishy.Runtime.SquishyPet;
using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Runtime
{
    /// <summary>
    /// Builds the home view from the save and content data: room at the current level's width,
    /// the favourite squishy at its size and finish, and the camera and tilt-shift focused on it.
    /// </summary>
    public sealed class HomeScene : MonoBehaviour
    {
        [SerializeField] private SteamerRoom room;
        [SerializeField] private SquishyBody squishy;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private TiltShiftController tiltShift;

        private void Start()
        {
            var game = GameBootstrap.Instance;
            if (game == null || game.Save == null)
            {
                Debug.LogError("HomeScene needs a GameBootstrap that has loaded a save.", this);
                return;
            }
            var content = game.Content;
            var save = game.Save;

            var level = Growth.RoomLevel(content.progression.roomLevels, save.roomLevel);
            room.Build(level != null ? level.relativeWidth : 0.77f);

            string favouriteId = string.IsNullOrEmpty(save.favouriteSquishyId)
                ? content.economy.def.startingSquishyId
                : save.favouriteSquishyId;
            var finish = content.finishes.Find(f => f.def.id == favouriteId);
            if (finish == null) Debug.LogWarning("No finish asset with id '" + favouriteId + "'.", this);

            int copies = Mathf.Max(1, Growth.CopiesOf(save.squishies, favouriteId));
            var size = Growth.SizeFor(content.progression.sizeTiers, copies);
            float scale = size != null ? size.relativeScale : 1f;

            squishy.Build(scale, finish != null ? finish.baseColor : Color.white, finish != null ? finish.smoothness : 0.5f);
            squishy.GetComponent<SquishyWander>().Init(room, scale);
            followCamera.Init(room, squishy.transform, squishy.Radius);
            if (tiltShift != null) tiltShift.SetFocus(squishy.transform, squishy.Radius);
        }
    }
}
