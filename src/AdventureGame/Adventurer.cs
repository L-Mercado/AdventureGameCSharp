namespace AdventureGame;

public class Adventurer
{
    private bool hasLamp;
    private bool hasKey;

    public Adventurer()
    {
        hasLamp = false;
        hasKey = false;
    }

    public bool HasLamp()
    {
        return hasLamp;
    }

    public bool HasKey()
    {
        return hasKey;
    }

    public void SetLamp(bool b)
    {
        hasLamp = b;
    }

    public void SetKey(bool b)
    {
        hasKey = b;
    }
}