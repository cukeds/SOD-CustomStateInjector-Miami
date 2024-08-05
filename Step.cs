namespace SODCustomStateInjectorMiami;

public class Step()
{
    public string Name;
    public CityConstructor.LoadState LoadState;
    
    // Override equals and hashcode
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        var s = (Step)obj;
        return Name.Equals(s.Name) && LoadState.Equals(s.LoadState);
    }
    
    public override int GetHashCode()
    {
        return Name.GetHashCode() + LoadState.GetHashCode();
    }
}

public class CustomStep 
{
    public Step Step;
    public Step After;
    
    // Override equals and hashcode
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        var s = (CustomStep)obj;
        return Step.Equals(s.Step) && After.Equals(s.After);
    }
    
    public override int GetHashCode()
    {
        return Step.GetHashCode() + After.GetHashCode();
    }
}

