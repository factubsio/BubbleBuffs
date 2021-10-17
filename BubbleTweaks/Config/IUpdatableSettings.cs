namespace BubbleBuffs.Config {
    public interface IUpdatableSettings {
        void OverrideSettings(IUpdatableSettings userSettings);
    }
}
