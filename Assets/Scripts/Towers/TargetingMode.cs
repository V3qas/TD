namespace TD.Towers
{
    public enum TargetingMode
    {
        First,
        Last,
        HighestHealth,
        LowestHealth,
        Fastest
    }

    public static class TargetingModeExtensions
    {
        public static string ToDisplayName(this TargetingMode mode)
        {
            switch (mode)
            {
                case TargetingMode.First:
                    return "First";
                case TargetingMode.Last:
                    return "Last";
                case TargetingMode.HighestHealth:
                    return "Most HP";
                case TargetingMode.LowestHealth:
                    return "Least HP";
                case TargetingMode.Fastest:
                    return "Fastest";
                default:
                    return mode.ToString();
            }
        }
    }
}
