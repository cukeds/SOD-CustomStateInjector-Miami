namespace SODCustomStateInjectorMiami;

public class Step()
{
    public string Name;
    public CityConstructor.LoadState LoadState;
}

public class CustomStep
{
    public Step Step;
    public Step After;
    public Step Original = new();
}
