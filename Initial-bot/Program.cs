using System;
using System.Threading.Tasks;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.IO;

namespace Initial_bot
{
    public class Program
    {
        private CommandService _commands;
        private DiscordSocketClient _client;
        private IServiceProvider _services;

        private static void Main(string[] args) => new Program().StartAsync().GetAwaiter().GetResult();

        public async Task StartAsync()
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();

            string tokenFile = Directory.GetCurrentDirectory() + "\\token.txt";
            var lines = File.ReadAllLines(tokenFile);
            string token = lines[0];

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();

            // Install commands
            await InstallCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Install events
            InstallEvents();

            Console.WriteLine("Bot Loaded!");

            await Task.Delay(-1);
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived Event into our Command Handler
            _client.MessageReceived += HandleCommandAsync;
            // Discover all of the commands in this assembly and load them.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());

            Console.WriteLine("Commands Installed!");
        }

        public void InstallEvents()
        {
            _client.UserJoined += AnnounceJoinedUser;
            _client.UserJoined += GiveJoinedUserRole;

            Console.WriteLine("Events Installed!");
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new SocketCommandContext(_client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await _commands.ExecuteAsync(context, argPos, _services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        [Summary("Echos a welcome message when a new member joins the server.")]
        public async Task AnnounceJoinedUser(SocketGuildUser user)
        {
            var guild = _client.GetGuild(319717933477658625);
            //var channel = guild.DefaultChannel;

            var channel = guild.TextChannels.Where(x => x.Id == 414196461052493826).FirstOrDefault();
            string message = "**A new retard has arrived!**\n";
            message += "Welcome " + user.Mention + " to the server!\n";

            await channel.SendMessageAsync(message);
        }

        [Summary("Gives a role to a new member.")]
        public async Task GiveJoinedUserRole(SocketGuildUser user)
        {
            // Get the test guild
            var guild = _client.GetGuild(319717933477658625);

            // Get the Retard role
            var role = guild.Roles.Where(x => x.Name.ToString() == "Retard").FirstOrDefault();
            
            // Assign the role to user
            await user.AddRoleAsync(role); // Doesn't work
        }
    }

    // Create a module with no prefix
    public class Info : ModuleBase<SocketCommandContext>
    {
        // ~say hello -> hello
        [Command("say")]
        [Summary("Echos a message.")]
        public async Task SayAsync([Remainder] [Summary("The text to echo")] string echo)
        {
            // ReplyAsync is a method on ModuleBase
            await ReplyAsync(echo);
        }

        // roles 
        [Command("roles")]
        [Summary("Prints all roles of a user.")]
        public async Task SquareAsync([Summary("The mention of a user")] string userMention)
        {
            // Get the userId
            ulong userId = GetUserIdFromMention(userMention);

            if(userId == 0)
            {
                await Context.Channel.SendMessageAsync("Error, please mention a user.");
            }
            else
            {
                // Get the user
                var user = Context.Guild.Users.Where(x => x.Id == userId).FirstOrDefault();

                await Context.Channel.SendMessageAsync($"Roles of: {user.Mention}");
                var message = "";
                foreach (var urole in user.Roles)
                {
                    message += urole.Name + "   " + urole.Id + "\n";
                }
                await Context.Channel.SendMessageAsync(message);
            }
        }

        // roles 
        [Command("role")]
        [Summary("Test adding to role.")]
        public async Task AddRole([Summary("The mention of a user")] string userMention)
        {
            ulong userId = (ulong)Context.Message.MentionedUsers.FirstOrDefault()?.Id; // get user id via mention

            if (userId == 0)
                await Context.Channel.SendMessageAsync("Error, please mention a user.");
            else
            {
                // Get role
                var role = Context.Guild.Roles.Where(r => r.Name == "Retard").FirstOrDefault();

                var user = Context.Guild.Users.Where(u => u.Id == userId).FirstOrDefault();

                if (user != null)
                    await user.AddRoleAsync(role);
            }
        }

        [Summary("Returns the userId of a userMention or 0 when failing")]
        private ulong GetUserIdFromMention(string userMention)
        {
            userMention = userMention.Trim('<', '@', '!', '>');
            if(ulong.TryParse(userMention, out ulong userId))
            {
                if(Context.Guild.Users.Where(x => x.Id == userId).FirstOrDefault() != null)
                {
                    return userId;
                }
            }
            return 0;
        }
    }

    // Create a module with the 'sample' prefix
    [Group("sample")]
    public class Sample : ModuleBase<SocketCommandContext>
    {
        // ~sample square 20 -> 400
        [Command("square")]
        [Summary("Squares a number.")]
        public async Task SquareAsync([Summary("The number to square.")] int num)
        {
            // We can also access the channel from the Command Context.
            await Context.Channel.SendMessageAsync($"{num}^2 = {Math.Pow(num, 2)}");
        }

        // ~sample userinfo --> foxbot#0282
        // ~sample userinfo @Khionu --> Khionu#8708
        // ~sample userinfo Khionu#8708 --> Khionu#8708
        // ~sample userinfo Khionu --> Khionu#8708
        // ~sample userinfo 96642168176807936 --> Khionu#8708
        // ~sample whois 96642168176807936 --> Khionu#8708
        [Command("userinfo")]
        [Summary("Returns info about the current user, or the user parameter, if one passed.")]
        [Alias("user", "whois")]
        public async Task UserInfoAsync([Summary("The (optional) user to get info for")] SocketUser user = null)
        {
            var userInfo = user ?? Context.Client.CurrentUser;
            await ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator}");
        }
    }
}