using System;
using System.Collections.Generic;
using System.Threading;
using RFRocketLibrary.Utils;
using Rocket.Core.Logging;
using Rocket.Unturned.Player;
using RocketExtensions.Utilities;

namespace RFRocketLibrary.Models
{
    // Original Author: ShimmyMySherbet
    /// <summary>
    /// Tool to watch for player key events. Remember to call Stop() when your finished with it, or it will continue to run.
    /// </summary>
    public class KeyListener
    {
        private readonly UnturnedPlayer _player;

        /// <summary>
        /// How many ms between updates. Lower value means higher accuracy, but too low and it could cause issues.
        /// </summary>
        private const int UpdateRate = 100;

        private volatile bool _isRunning;
        private readonly Dictionary<int, bool> _lastMapping = new();

        public delegate void Key(UnturnedPlayer player, UnturnedKey key);
        public delegate void KeyUpdated(UnturnedPlayer player, UnturnedKey key, bool isPressed);

        public event KeyUpdated? OnKeyUpdated;

        public event Key? OnKeyUp;

        public event Key? OnKeyDown;

        public bool JumpKeyDown => IsKeyDown(UnturnedKey.Jump);
        public bool SprintKeyDown => IsKeyDown(UnturnedKey.Sprint);
        public bool LPunchKeyDown => IsKeyDown(UnturnedKey.LPunch);
        public bool RPunchKeyDown => IsKeyDown(UnturnedKey.RPunch);
        public bool ProneKeyDown => IsKeyDown(UnturnedKey.Prone);
        public bool CrouchKeyDown => IsKeyDown(UnturnedKey.Crouch);
        public bool LeanLeftKeyDown => IsKeyDown(UnturnedKey.LeanLeft);
        public bool LeanRightKeyDown => IsKeyDown(UnturnedKey.LeanRight);
        public bool CodeHotkey1Down => IsKeyDown(UnturnedKey.CodeHotkey1);
        public bool CodeHotkey2Down => IsKeyDown(UnturnedKey.CodeHotkey2);
        public bool CodeHotkey3Down => IsKeyDown(UnturnedKey.CodeHotkey3);
        public bool CodeHotkey4Down => IsKeyDown(UnturnedKey.CodeHotkey4);
        public bool CodeHotkey5Down => IsKeyDown(UnturnedKey.CodeHotkey5);

        public KeyListener(UnturnedPlayer player)
        {
            _player = player;
            _isRunning = true;
            var runThread = new Thread(UpdateLoop);
            runThread.IsBackground = true;
            runThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>
        /// Returns the last polled state of a key, or false before the first poll
        /// (the mapping used to be read with a direct indexer, which threw before
        /// the polling thread had seeded it).
        /// </summary>
        public bool IsKeyDown(UnturnedKey key)
        {
            lock (_lastMapping)
            {
                return _lastMapping.TryGetValue((int) key, out var isDown) && isDown;
            }
        }

        private void UpdateLoop()
        {
            while (_isRunning)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception e)
                {
                    // Reading input of a player whose connection or Player object is already
                    // gone must not take the server down (the loop used to run as async void,
                    // so any exception here crashed the whole process). Stop watching instead;
                    // plugins can construct a new KeyListener for reconnecting players.
                    Logger.LogWarning($"[KeyListener] Stopped watching '{_player.DisplayName}': {e.Message}");
                    return;
                }

                Thread.Sleep(UpdateRate);
            }
        }

        private void PollOnce()
        {
            var input = _player.Player.input;
            // The previous loop stopped one element short (Length - 1) and never polled
            // the last slot; unmapped slots are filtered out by IntToUKey anyway.
            for (var i = 0; i < input.keys.Length; i++)
            {
                var current = input.keys[i];
                bool last;
                lock (_lastMapping)
                {
                    if (!_lastMapping.TryGetValue(i, out last))
                    {
                        _lastMapping[i] = current;
                        continue;
                    }
                }

                if (last == current)
                    continue;

                var key = IntToUKey(i);
                lock (_lastMapping)
                {
                    _lastMapping[i] = current;
                }

                if (key == UnturnedKey.Unknown)
                    continue;

                if (current)
                {
                    ThreadTool.RunOnGameThreadAsync(() => OnKeyUpdated?.Invoke(_player, key, current)).Forget();
                    ThreadTool.RunOnGameThreadAsync(() => OnKeyDown?.Invoke(_player, key)).Forget();
                }
                else
                {
                    ThreadTool.RunOnGameThreadAsync(() => OnKeyUpdated?.Invoke(_player, key, current)).Forget();
                    ThreadTool.RunOnGameThreadAsync(() => OnKeyUp?.Invoke(_player, key)).Forget();
                }
            }
        }

        private static UnturnedKey IntToUKey(int i)
        {
            return i switch
            {
                0 => UnturnedKey.Jump,
                1 => UnturnedKey.LPunch,
                2 => UnturnedKey.RPunch,
                3 => UnturnedKey.Crouch,
                4 => UnturnedKey.Prone,
                5 => UnturnedKey.Sprint,
                6 => UnturnedKey.LeanLeft,
                7 => UnturnedKey.LeanRight,
                9 => UnturnedKey.CodeHotkey1,
                10 => UnturnedKey.CodeHotkey2,
                11 => UnturnedKey.CodeHotkey3,
                12 => UnturnedKey.CodeHotkey4,
                13 => UnturnedKey.CodeHotkey5,
                _ => UnturnedKey.Unknown
            };
        }
    }

    public enum UnturnedKey
    {
        Unknown = -1,
        Jump = 0,
        LPunch = 1,
        RPunch = 2,
        /// <summary>
        /// WARNING: Key status will be down while the player is crouched, even if they are not holding the key down.
        /// </summary>
        Crouch = 3,
        /// <summary>
        /// WARNING: Key status will be down while the player is prone, even if they are not holding the key down.
        /// </summary>
        Prone = 4,
        Sprint = 5,
        LeanLeft = 6,
        LeanRight = 7,
        /// <summary>
        /// Defaults to comma
        /// </summary>
        CodeHotkey1 = 9,
        /// <summary>
        /// Defaults to period
        /// </summary>
        CodeHotkey2 = 10,
        /// <summary>
        /// Defaults to forward slash
        /// </summary>
        CodeHotkey3 = 11,
        /// <summary>
        /// Defaults to semicolon
        /// </summary>
        CodeHotkey4 = 12,
        /// <summary>
        /// Defaults to quote
        /// </summary>
        CodeHotkey5 = 13,
    }
}
