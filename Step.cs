namespace SODCustomStateInjectorMiami;

public class Step()
{
    public string Name;
    public int LoadState;
}

public class CustomStep
{
    public Step Step;
    public Step After;
}