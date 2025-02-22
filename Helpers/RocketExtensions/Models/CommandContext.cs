﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RFRocketLibrary.Helpers;
using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core;
using Rocket.Unturned.Player;
using RocketExtensions.Models.Exceptions;
using RocketExtensions.Plugins;
using RocketExtensions.Utilities;
using UnityEngine;

namespace RocketExtensions.Models
{
    /// <summary>
    /// Contains the context for command execution
    /// </summary>
    public struct CommandContext
    {
        public IRocketPlayer Player { get; }
        public string[] CommandRawArguments { get; }
        public ArgumentList Arguments { get; }

        public ulong PlayerID => LDMPlayer.PlayerID;

        public bool IsConsole => LDMPlayer.IsConsole;

        public LDMPlayer LDMPlayer { get; }

        /// <summary>
        /// The caller as an UnturnedPlayer. Null when called from console.
        /// </summary>
        public UnturnedPlayer? UnturnedPlayer => Player as UnturnedPlayer;

        public RocketCommand Command { get; }

        public CommandContext(IRocketPlayer player, RocketCommand callingCommand, string[] args)
        {
            Command = callingCommand;
            Player = player;
            CommandRawArguments = args;
            Arguments = new ArgumentList(args);
            LDMPlayer = LDMPlayer.FromRocketPlayer(player);
        }

        /// <summary>
        /// Sends a message to the caller
        /// </summary>
        public async Task ReplyAsync(string message, Color? messageColor = null, string? iconUrl = null)
        {
            messageColor ??= Color.green;
            message = message.ReformatColor();
            await ThreadTool.RunOnGameThreadAsync(ChatHelper.Say, Player, message, messageColor, iconUrl);
        }

        /// <summary>
        /// Translates and sends the specified message to the command caller.
        /// </summary>
        /// <param name="translationKey">Translations key as set in your plugin's Translations</param>
        /// <param name="arguments"></param>
        public async Task ReplyKeyAsync(string translationKey, params object[] arguments)
        {
            if (Command.Plugin == null)
                throw new PluginNotFoundException($"Failed to find plugin instance for assembly {GetType().Assembly.GetName().Name}");
            
            var translated = Command.Plugin.DefaultTranslations.Translate(translationKey, arguments);
            await ReplyAsync(translated);
        }

        /// <summary>
        /// Sends a message to the specified player
        /// </summary>
        public async Task SayAsync(IRocketPlayer player, string message, Color? messageColor = null, string? iconUrl = null)
        {
            messageColor ??= Color.green;
            message = message.ReformatColor();
            await ThreadTool.RunOnGameThreadAsync(ChatHelper.Say, player, message, messageColor, iconUrl);
        }

        /// <summary>
        /// Sends a message to all online players.
        /// </summary>
        public async Task AnnounceAsync(string message, Color? messageColor = null, string? iconUrl = null)
        {
            messageColor ??= Color.green;
            message = message.ReformatColor();
            await ThreadTool.RunOnGameThreadAsync(ChatHelper.Broadcast, message, messageColor, iconUrl);
        }

        /// <summary>
        /// Cancels the command cooldown for the current command.
        /// </summary>
        /// <returns>True if the cooldown was found and cancelled</returns>
        public async Task<bool> CancelCooldownAsync()
        {
            return await CooldownManager.CancelCooldownAsync(Player, Command);
        }

        /// <summary>
        /// Sets a command cooldown for the current command
        /// </summary>
        /// <param name="cooldown">Cooldown time in seconds</param>
        /// <returns>True if a new cooldown was created, or false if an existing one was updated</returns>
        public async Task<bool> SetCooldownAsync(uint cooldown)
        {
            return await CooldownManager.SetCooldownAsync(Player, Command, cooldown);
        }

        /// <summary>
        /// Checks if a player has a permission. Runs async on the game thread.
        /// </summary>
        public async Task<bool> CheckPermissionAsync(string permission)
        {
            return await ThreadTool.RunOnGameThreadAsync(Player.HasPermission, permission);
        }

        public async Task<List<Permission>?> GetPermissionsAsync()
        {
            return await ThreadTool.RunOnGameThreadAsync(R.Permissions.GetPermissions, Player);
        }

        public async Task<string[]> GetPermissionKeysAsync()
        {
            return (await GetPermissionsAsync() ?? new List<Permission>())
                .Select(x => x.Name)
                .ToArray();
        }
    }
}