using CommandTerminal;
using Lexone.UnityTwitchChat;

public static class TwitchIrcCommands
{
    [RegisterCommand(Name = "twitch.channel", Help = "Shows or changes the Twitch IRC channel. Usage: twitch.channel <channel>", MinArgCount = 0, MaxArgCount = 1)]
    private static void CommandTwitchChannel(CommandArg[] args)
    {
        IRC irc = IRC.Instance;
        if (irc == null)
        {
            Terminal.Shell.IssueErrorMessage("Twitch IRC instance was not found.");
            return;
        }

        if (args.Length == 0)
        {
            Terminal.Log("Twitch IRC channel: {0}", string.IsNullOrEmpty(irc.channel) ? "(none)" : irc.channel);
            return;
        }

        string nextChannel = NormalizeChannel(args[0].String);
        if (string.IsNullOrEmpty(nextChannel))
        {
            Terminal.Shell.IssueErrorMessage("Channel name cannot be empty.");
            return;
        }

        string previousChannel = irc.channel;
        if (string.Equals(previousChannel, nextChannel, System.StringComparison.OrdinalIgnoreCase))
        {
            Terminal.Log("Twitch IRC is already set to #{0}.", nextChannel);
            return;
        }

        irc.channel = nextChannel;
        irc.Connect();

        Terminal.Log(
            "Twitch IRC channel changed from #{0} to #{1}. Reconnecting...",
            string.IsNullOrEmpty(previousChannel) ? "(none)" : previousChannel,
            nextChannel);
    }

    private static string NormalizeChannel(string rawChannel)
    {
        if (string.IsNullOrWhiteSpace(rawChannel))
            return string.Empty;

        string channel = rawChannel.Trim();
        if (channel.StartsWith("#"))
            channel = channel.Substring(1);

        int slashIndex = channel.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < channel.Length - 1)
            channel = channel.Substring(slashIndex + 1);

        int queryIndex = channel.IndexOf('?');
        if (queryIndex >= 0)
            channel = channel.Substring(0, queryIndex);

        return channel.Trim().ToLowerInvariant();
    }
}
