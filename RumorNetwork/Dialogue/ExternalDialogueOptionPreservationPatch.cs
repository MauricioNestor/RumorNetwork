using Vintagestory.API.Common;

namespace RumorNetwork.Dialogue
{
    public sealed class ExternalDialogueOptionPreservationPatch :
        ModSystem
    {
        public override double ExecuteOrder()
        {
            return -0.77;
        }

        public override void Start(ICoreAPI api)
        {
            // The canonical-root integration now returns directly to the
            // dialogue's real persistent menu. Rebuilding another mod's answer
            // array is unsafe because conditional responses are evaluated by
            // the dialogue controller and must remain under its ownership.
            api.Logger.Notification(
                "Rumor Network deixou a avaliação das opções externas " +
                "sob controle do diálogo original."
            );
        }
    }
}
