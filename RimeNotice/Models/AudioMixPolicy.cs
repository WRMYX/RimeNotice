namespace RimeNotice.Models;
public static class AudioMixPolicy
{
    public static (double Tone, double Speech) Gains(AudioOptions options, bool simultaneous) => !simultaneous ? (1,1) : options.MixMode switch {
        AudioMixMode.SpeechPriority => (options.DuckingGain,1),
        AudioMixMode.ReminderPriority => (1,options.DuckingGain),
        _ => (1,1)
    };
    public static string Text(string sender,string message,AudioOptions options)
    {
        if(options.ReadSender && options.ReadMessage) return sender+"。\n"+message;
        return options.ReadSender?sender:options.ReadMessage?message:"";
    }
}
