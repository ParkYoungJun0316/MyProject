public enum StageProgressState
{
    Locked = 0,
    Unlocked = 1,
    Cleared = 2
}

public enum StageFlowSceneRole
{
    Stage = 0,
    Cutscene = 1
}

public interface IPlayerContext
{
    int PlayerId { get; }
}

public interface IDamageReceiver
{
    void ReceiveDamage(int amount, object source);
}
