﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Native;

namespace GTACoOp
{
    public class Chat
    {
        public event EventHandler OnComplete;

        public Chat()
        {
            CurrentInput = "";
            _mainScaleform = new Scaleform("multiplayer_chat");
        }

        public bool HasInitialized;

        public void Init()
        {
            _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "ALL");
            _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "ALL");

            var safezone = Function.Call<float>(Hash.GET_SAFE_ZONE_SIZE);
            var widescreen = Function.Call<bool>(Hash.GET_IS_WIDESCREEN);
            _mainScaleform.CallFunction(
                "SET_DISPLAY_CONFIG", GTA.UI.Screen.Width, GTA.UI.Screen.Height, safezone, safezone, safezone, safezone, widescreen, false);

            HasInitialized = true;
        }

        public bool IsFocused
        {
            get { return _isFocused; }
            set
            {
                if (value && !_isFocused)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 2, 2, "ALL");
                }
                else if (!value && _isFocused)
                {
                    _mainScaleform.CallFunction("SET_FOCUS", 1, 2, "ALL");
                }

                _isFocused = value;

                if (value && _isHidden)
                    _isHidden = false;
            }
        }

        private readonly Scaleform _mainScaleform;

        public string CurrentInput;

        private int _switch = 1;
        private Keys _lastKey;
        private bool _isFocused;
        private DateTime _lastMessageTime = DateTime.UtcNow;
        private bool _isHidden = false;

        public void Tick()
        {
            if (!Main.IsOnServer()) return;

            if (_lastMessageTime.AddSeconds(15) < DateTime.UtcNow && !IsFocused && !_isHidden)
            {
                _mainScaleform.CallFunction("hide");
                _isHidden = true;
            }

            if (!_isHidden)
                _mainScaleform.Render2D();


            if (!IsFocused) return;
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
        }

        public void AddMessage(string sender, string msg)
        {
            _lastMessageTime = DateTime.UtcNow;
            if (_isHidden)
            {
                _mainScaleform.CallFunction("showFeed");
                _isHidden = false;
            }

            if (string.IsNullOrEmpty(sender))
            {
                _mainScaleform.CallFunction("ADD_MESSAGE", "", msg);
                if(Main.PlayerSettings.ChatLog)
                    System.IO.File.AppendAllText("scripts\\GTACOOP_chat.log", "[" + DateTime.UtcNow + "] " + msg + "\n");
            }
            else
            {
                _mainScaleform.CallFunction("ADD_MESSAGE", sender + ":", msg);
                if (Main.PlayerSettings.ChatLog)
                    System.IO.File.AppendAllText("scripts\\GTACOOP_chat.log", "[" + DateTime.UtcNow + "] " + sender + ": " + msg + "\n");
            }
        }

        public void Reset()
        {
            _mainScaleform.CallFunction("RESET");
        }

        public void OnKeyDown(Keys key)
        {
            if (key == Keys.PageUp && Main.IsOnServer())
                _mainScaleform.CallFunction("PAGE_UP");

            else if (key == Keys.PageDown && Main.IsOnServer())
                _mainScaleform.CallFunction("PAGE_DOWN");

            if (!IsFocused) return;

            if ((key == Keys.ShiftKey && _lastKey == Keys.Menu) || (key == Keys.Menu && _lastKey == Keys.ShiftKey))
                ActivateKeyboardLayout(1, 0);

            _lastKey = key;

            if (key == Keys.Escape)
            {
                IsFocused = false;
                CurrentInput = "";
            }

            var keyChar = GetCharFromKey(key, Game.IsKeyPressed(Keys.ShiftKey), false);

            if (keyChar.Length == 0) return;

            if (keyChar[0] == (char)8)
            {
                if (CurrentInput.Length > 0)
                {
                    CurrentInput = CurrentInput.Remove(CurrentInput.Length - 1);
                    _mainScaleform.CallFunction("DELETE_TEXT");
                }
                return;
            }
            if (keyChar[0] == (char)13)
            {
                _mainScaleform.CallFunction("ADD_TEXT", "ENTER");
                if (OnComplete != null) OnComplete.Invoke(this, EventArgs.Empty);
                CurrentInput = "";
                return;
            }
            var str = keyChar;

            CurrentInput += str;
            _mainScaleform.CallFunction("ADD_TEXT", str);
        }


        [DllImport("user32.dll")]
        public static extern int ToUnicodeEx(uint virtualKeyCode, uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
                StringBuilder receivingBuffer,
        int bufferSize, uint flags, IntPtr kblayout);

        [DllImport("user32.dll")]
        public static extern int ActivateKeyboardLayout(int hkl, uint flags);

        public static string GetCharFromKey(Keys key, bool shift, bool altGr)
        {
            var buf = new StringBuilder(256);
            var keyboardState = new byte[256];
            if (shift)
                keyboardState[(int)Keys.ShiftKey] = 0xff;
            if (altGr)
            {
                keyboardState[(int)Keys.ControlKey] = 0xff;
                keyboardState[(int)Keys.Menu] = 0xff;
            }

            ToUnicodeEx((uint)key, 0, keyboardState, buf, 256, 0, InputLanguage.CurrentInputLanguage.Handle);
            return buf.ToString();
        }
    }
}