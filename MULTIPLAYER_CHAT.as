class com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT extends com.rockstargames.ui.core.BaseScreenLayout
{
   static var VISIBLE_STATE_HIDDEN = 0;
   static var VISIBLE_STATE_DEFAULT = 1;
   static var VISIBLE_STATE_TYPING = 2;
   static var FOCUS_MODE_LOBBY = 0;
   static var FOCUS_MODE_GAME = 1;
   static var DEFAULT_COLOUR = com.rockstargames.ui.utils.HudColour.HUD_COLOUR_PURE_WHITE;
   static var MAX_HISTORY_SIZE = 20;
   function MULTIPLAYER_CHAT()
   {
      super();
      _global.gfxExtensions = true;
      this.DispConf = new com.rockstargames.ui.utils.DisplayConfig();
      this.DispConf = this.getDisplayConfig(true);
   }
   function INITIALISE(mc)
   {
      super.INITIALISE(mc);
      this.feeds = [];
      this.history = new Array(com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.MAX_HISTORY_SIZE);
      this.historyIndex = 0;
      this.hudColour = new com.rockstargames.ui.utils.HudColour();
      this.eHudColour = com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.DEFAULT_COLOUR;
      this.player = "";
      this.scope = "";
      this.focusMode = com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.FOCUS_MODE_GAME;
      this.initLobbyFeed();
      this.initGameFeed();
      this.initTextInput();
   }
   function SET_DISPLAY_CONFIG(_screenWidthPixels, _screenHeightPixels, _safeTopPercent, _safeBottomPercent, _safeLeftPercent, _safeRightPercent, _isWideScreen, _isCircleAccept)
   {
      this.DispConf.isCircleAccept = _isCircleAccept;
      this.DispConf.isWideScreen = _isWideScreen;
      this.DispConf.safeBottom = _safeBottomPercent;
      this.DispConf.safeLeft = _safeLeftPercent;
      this.DispConf.safeRight = _safeRightPercent;
      this.DispConf.safeTop = _safeTopPercent;
      this.DispConf.screenHeight = _screenHeightPixels;
      this.DispConf.screenWidth = _screenWidthPixels;
      var _loc3_ = 412;
      var _loc4_ = this.DispConf.safeRight * this.DispConf.screenWidth - _loc3_;
      var _loc2_ = 452;
      var _loc6_ = 25;
      var _loc7_ = this.DispConf.safeRight * this.DispConf.screenWidth - _loc2_;
      var _loc5_ = this.DispConf.safeBottom * this.DispConf.screenHeight - _loc6_;
      this.feeds[0].RepositionMoviePostScreeenAdjust(this.DispConf.safeLeft * this.DispConf.screenWidth,606);
      this.feeds[1].RepositionMoviePostScreeenAdjust(_loc4_,365);
      this.textInput.RepositionMoviePostScreeenAdjust(_loc7_,_loc5_);
   }
   function initGameFeed()
   {
      var _loc3_ = 4;
      var _loc2_ = 412;
      var _loc5_ = this.DispConf.safeRight * this.DispConf.screenWidth - _loc2_;
      var _loc4_ = 365;
      this.feeds.push(new com.rockstargames.gtav.Multiplayer.textchat.Feed(this.CONTENT,_loc5_,_loc4_,_loc2_,"right",_loc3_));
   }
   function initLobbyFeed()
   {
      var _loc2_ = 2;
      var _loc3_ = 412;
      var _loc5_ = this.DispConf.safeLeft * this.DispConf.screenWidth;
      var _loc4_ = 606;
      this.feeds.push(new com.rockstargames.gtav.Multiplayer.textchat.Feed(this.CONTENT,_loc5_,_loc4_,_loc3_,"left",_loc2_));
   }
   function initTextInput()
   {
      var _loc2_ = 452;
      var _loc3_ = 25;
      var _loc5_ = this.DispConf.safeRight * this.DispConf.screenWidth - _loc2_;
      var _loc4_ = this.DispConf.safeBottom * this.DispConf.screenHeight - _loc3_;
      this.textInput = new com.rockstargames.gtav.Multiplayer.textchat.Input(this.CONTENT,_loc5_,_loc4_);
   }
   function hide()
   {
      this.scope = "";
      this.player = "";
      this.eHudColour = com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.DEFAULT_COLOUR;
      this.textInput.reset();
      this.textInput.hide();
      var _loc2_ = 0;
      var _loc3_ = this.feeds.length;
      while(_loc2_ < _loc3_)
      {
         this.feeds[_loc2_].hide();
         _loc2_ = _loc2_ + 1;
      }
   }
   function showInput(scope, eHudColour)
   {
      this.scope = scope;
      this.textInput.show();
      this.textInput.clear();
      this.textInput.setPrefix(scope);
      this.eHudColour = eHudColour;
   }
   function showFeed()
   {
      this.textInput.hide();
      if((var _loc0_ = this.focusMode) !== com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.FOCUS_MODE_LOBBY)
      {
         this.feeds[0].hide();
         this.feeds[1].show();
      }
      else
      {
         this.feeds[0].show();
         this.feeds[1].hide();
      }
   }
   function historyUp()
   {
      var _loc4_ = 0;
      var _loc6_ = this.feeds.length;
      while(_loc4_ < _loc6_)
      {
         var _loc2_ = this.feeds[_loc4_];
         if(_loc2_.historyOffset > - com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.MAX_HISTORY_SIZE)
         {
            var _loc5_ = (this.historyIndex - _loc2_.__get__maxLines() + _loc2_.historyOffset + com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.MAX_HISTORY_SIZE) % com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.MAX_HISTORY_SIZE;
            if(_loc5_ != this.historyIndex - 1)
            {
               var _loc3_ = this.history[_loc5_];
               if(_loc3_ != undefined)
               {
                  com.rockstargames.ui.utils.Colour.setHudColour(_loc3_.hudColour,this.hudColour);
                  var _loc7_ = this.hudColour.r << 16 | this.hudColour.g << 8 | this.hudColour.b;
                  _loc2_.addLine(_loc3_.player,_loc3_.scope,_loc3_.message,_loc7_,true,false);
                  _loc2_.historyOffset = _loc2_.historyOffset - 1;
               }
            }
         }
         _loc4_ = _loc4_ + 1;
      }
   }
   function historyDown(forceFinishTweens)
   {
      var _loc8_ = true;
      var _loc4_ = 0;
      var _loc5_ = this.feeds.length;
      while(_loc4_ < _loc5_)
      {
         var _loc2_ = this.feeds[_loc4_];
         if(_loc2_.historyOffset < 0)
         {
            var _loc7_ = (this.historyIndex + this.history.length + _loc2_.historyOffset) % com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.MAX_HISTORY_SIZE;
            var _loc3_ = this.history[_loc7_];
            com.rockstargames.ui.utils.Colour.setHudColour(_loc3_.hudColour,this.hudColour);
            var _loc6_ = this.hudColour.r << 16 | this.hudColour.g << 8 | this.hudColour.b;
            _loc2_.addLine(_loc3_.player,_loc3_.scope,_loc3_.message,_loc6_,false,forceFinishTweens);
            _loc2_.historyOffset = _loc2_.historyOffset + 1;
            _loc8_ = false;
         }
         _loc4_ = _loc4_ + 1;
      }
      return _loc8_;
   }
   function ADD_TEXT(text)
   {
      this.textInput.addText(text);
   }
   function DELETE_TEXT()
   {
      this.textInput.backspace();
   }
   function ABORT_TEXT()
   {
      this.textInput.clear();
   }
   function COMPLETE_TEXT()
   {
      var _loc2_ = this.textInput.getInput();
      this.ADD_MESSAGE(this.player,_loc2_,this.scope,false,this.eHudColour);
   }
   function SET_TYPING_DONE()
   {
      this.COMPLETE_TEXT();
   }
   function ADD_MESSAGE(player, message, scope, teamOnly, eHudColour)
   {
      do
      {
         var showingMostRecentMessage = this.historyDown(true);
      }
      while(!showingMostRecentMessage);
      
      com.rockstargames.ui.utils.Colour.setHudColour(eHudColour,this.hudColour);
      var _loc4_ = this.hudColour.r << 16 | this.hudColour.g << 8 | this.hudColour.b;
      var _loc2_ = 0;
      var _loc3_ = this.feeds.length;
      while(_loc2_ < _loc3_)
      {
         this.feeds[_loc2_].addLine(player,scope,message,_loc4_,false,false);
         _loc2_ = _loc2_ + 1;
      }
      this.history[this.historyIndex] = {player:player,scope:scope,message:message,hudColour:eHudColour};
      this.historyIndex = (this.historyIndex + 1) % com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.MAX_HISTORY_SIZE;
   }
   function SET_FOCUS(eVisibleState, scopeType, scope, player, eHudColour)
   {
      this.player = player;
      switch(eVisibleState)
      {
         case com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.VISIBLE_STATE_HIDDEN:
            this.hide();
            break;
         case com.rockstargames.gtav.Multiplayer.textchat.MULTIPLAYER_CHAT.VISIBLE_STATE_TYPING:
            this.showFeed();
            this.showInput(scope,eHudColour);
            break;
         default:
            this.showFeed();
      }
   }
   function SET_FOCUS_MODE(focusMode)
   {
      this.focusMode = focusMode;
   }
   function PAGE_UP()
   {
      this.historyUp();
   }
   function PAGE_DOWN()
   {
      this.historyDown(false);
   }
   function RESET()
   {
      this.history = [];
      this.hide();
      var _loc2_ = 0;
      var _loc3_ = this.feeds.length;
      while(_loc2_ < _loc3_)
      {
         this.feeds[_loc2_].reset();
         _loc2_ = _loc2_ + 1;
      }
   }
}
