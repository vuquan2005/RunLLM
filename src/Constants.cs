namespace Community.PowerToys.Run.Plugin.RunLLM
{
    /// <summary>
    /// Default values and constants for the RunLLM plugin.
    /// </summary>
    public static class Constants
    {
        public const string PluginID = "0167343682284415AF592A37253E75AA";
        public const string PluginName = "RunLLM";
        public const string PluginDescription = "Chat with LLMs directly from PowerToys Run";

        // Default settings
        public const string DefaultUrl = "http://localhost:11434";
        public const string DefaultModel = "qwen/qwen3-4b";
        public const string DefaultSystemPrompt = "";

        // API endpoints
        public const string ModelsEndpoint = "/v1/models";
        public const string ChatCompletionsEndpoint = "/v1/chat/completions";

        // Settings keys
        public const string SettingKeyUrl = "LLMUrl";
        public const string SettingKeyModel = "DfModel";
        public const string SettingKeySystemPrompt = "SystemPrompt";
        public const string SettingKeyApiKey = "ApiKey";
        public const string SettingKeyEnableThinking = "EnableThinking";
        public const string SettingKeyReasoningEffort = "ReasoningEffort";
        public const string SettingKeyThinkingModeType = "ThinkingModeType";

        // Reasoning effort levels
        public static readonly string[] ReasoningEffortLevels = ["none", "minimal", "low", "medium", "high"];

        // Cache durations
        public const int ModelsCacheMinutes = 1;

        // Icon paths
        public const string IconModel = "Images/model.png";
        public const string IconRun = "Images/run.png";
        public const string IconChange = "Images/change.png";
        public const string IconBrain = "Images/brain.png";
        public const string IconTimer = "Images/timer.png";
        public const string IconTransfer = "Images/transfer.png";
        public const string IconAccess = "Images/access.png";
    }
}
