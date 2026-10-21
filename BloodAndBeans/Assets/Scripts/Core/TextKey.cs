// 생성 파일이 아니다 — 키를 더하면 Localization.xlsx의 Key 열과 같이 고친다.
// `TextKeyTests`가 이 상수가 전부 표에 있는지 검사한다.

/// `L10n`에 넘기는 문구 키. 값은 `Localization.xlsx`가 원본이고, 여기는 키의 철자만 한 곳에 모은다.
public static class TextKey
{
    public static class Hud
    {
        public const string Day = "Hud.Day";
        public const string Finished = "Hud.Finished";
        public const string Host = "Hud.Host";
        public const string Ping = "Hud.Ping";
        public const string TeamRevenueNone = "Hud.TeamRevenueNone";
        public const string TeamRevenue = "Hud.TeamRevenue";
        public const string BagCapacity = "Hud.BagCapacity";
        public const string BagNone = "Hud.BagNone";
        public const string BagReissue = "Hud.BagReissue";
        public const string TodaySales = "Hud.TodaySales";
        public const string Denied = "Hud.Denied";
        public const string TomorrowCustomers = "Hud.TomorrowCustomers";
        public const string PopularIngredients = "Hud.PopularIngredients";
        public const string HandItem = "Hud.HandItem";
        public const string MateItem = "Hud.MateItem";
        public const string DashOverload = "Hud.DashOverload";
        public const string ReturnMarker = "Hud.ReturnMarker";
        public const string PhaseNight = "Hud.PhaseNight";
        public const string PhaseTransition = "Hud.PhaseTransition";
        public const string PhaseDay = "Hud.PhaseDay";
        public const string Stocked = "Hud.Stocked";
    }

    public static class Select
    {
        public const string NotAllReady = "Select.NotAllReady";
        public const string TeamFull = "Select.TeamFull";
        public const string SkillHeader = "Select.SkillHeader";
        public const string NightActiveHint = "Select.NightActiveHint";
        public const string CrewReady = "Select.CrewReady";
        public const string StartGame = "Select.StartGame";
        public const string CancelReady = "Select.CancelReady";
        public const string Ready = "Select.Ready";
        public const string ScrollOn = "Select.ScrollOn";
        public const string ScrollOff = "Select.ScrollOff";
        public const string TeamSlot = "Select.TeamSlot";
        public const string RosterWaiting = "Select.RosterWaiting";
        public const string RosterReady = "Select.RosterReady";
        public const string RosterNotReady = "Select.RosterNotReady";
    }

    public static class Room
    {
        public const string Status = "Room.Status";
        public const string Team = "Room.Team";
        public const string ReadyAction = "Room.ReadyAction";
        public const string CancelReadyAction = "Room.CancelReadyAction";
        public const string TeamOccupancy = "Room.TeamOccupancy";
        public const string TeamUnset = "Room.TeamUnset";
        public const string HostTag = "Room.HostTag";
        public const string ReadyTag = "Room.ReadyTag";
        public const string WaitTag = "Room.WaitTag";
        public const string ReadyCount = "Room.ReadyCount";
    }

    public static class Loading
    {
        public const string WaitingPlayers = "Loading.WaitingPlayers";
        public const string Default = "Loading.Default";
    }

    public static class BoxLoot
    {
        public const string BagBuried = "BoxLoot.BagBuried";
        public const string SlotSummary = "BoxLoot.SlotSummary";
        public const string Revealing = "BoxLoot.Revealing";
        public const string AllRevealed = "BoxLoot.AllRevealed";
        public const string SlotHidden = "BoxLoot.SlotHidden";
        public const string SlotEmpty = "BoxLoot.SlotEmpty";
        public const string RarityCommon = "BoxLoot.RarityCommon";
        public const string RarityRare = "BoxLoot.RarityRare";
    }

    public static class Gauge
    {
        public const string Baking = "Gauge.Baking";
        public const string Brewing = "Gauge.Brewing";
        public const string Target = "Gauge.Target";
        public const string Waiting = "Gauge.Waiting";
    }

    public static class Settlement
    {
        public const string Countdown = "Settlement.Countdown";
        public const string DayHeading = "Settlement.DayHeading";
        public const string Paid = "Settlement.Paid";
        public const string Unpaid = "Settlement.Unpaid";
        public const string MissStreak = "Settlement.MissStreak";
        public const string OurTeam = "Settlement.OurTeam";
    }

    public static class Making
    {
        public const string InProgress = "Making.InProgress";
        public const string Trash = "Making.Trash";
        public const string Burnt = "Making.Burnt";
        public const string Servable = "Making.Servable";
        public const string CompletesWhenCooked = "Making.CompletesWhenCooked";
        public const string AddMore = "Making.AddMore";
        public const string PourOut = "Making.PourOut";
    }

    public static class PhaseCue
    {
        public const string Ready = "PhaseCue.Ready";
        public const string Go = "PhaseCue.Go";
        public const string Closing = "PhaseCue.Closing";
    }

    public static class Result
    {
        public const string Caption = "Result.Caption";
        public const string Draw = "Result.Draw";
        public const string TeamWin = "Result.TeamWin";
        public const string MatchEnded = "Result.MatchEnded";
        public const string NoDailySales = "Result.NoDailySales";
    }

    public static class Return
    {
        public const string SuccessTitle = "Return.SuccessTitle";
        public const string SuccessLine = "Return.SuccessLine";
        public const string SuccessWithLostBag = "Return.SuccessWithLostBag";
        public const string SuccessKept = "Return.SuccessKept";
        public const string SuccessNone = "Return.SuccessNone";
        public const string PartialTitle = "Return.PartialTitle";
        public const string PartialLine = "Return.PartialLine";
        public const string PartialDetail = "Return.PartialDetail";
        public const string LostTitle = "Return.LostTitle";
        public const string LostLine = "Return.LostLine";
        public const string LostBagWithCount = "Return.LostBagWithCount";
        public const string LostBag = "Return.LostBag";
    }

    public static class RoomList
    {
        public const string TeamCount = "RoomList.TeamCount";
        public const string NoSelection = "RoomList.NoSelection";
    }

    public static class Recipe
    {
        public const string GemTurns = "Recipe.GemTurns";
        public const string GemTurnsRefreshed = "Recipe.GemTurnsRefreshed";
    }

    public static class Prompt
    {
        public const string Serve = "Prompt.Serve";
        public const string TakePlate = "Prompt.TakePlate";
        public const string TakeCup = "Prompt.TakeCup";
        public const string ShelfFinish = "Prompt.ShelfFinish";
        public const string ShelfOpen = "Prompt.ShelfOpen";
        public const string ShelfTitle = "Prompt.ShelfTitle";
        public const string ShelfHint = "Prompt.ShelfHint";
        public const string PrepEmpty = "Prompt.PrepEmpty";
        public const string PrepCount = "Prompt.PrepCount";
        public const string FacilityLine = "Prompt.FacilityLine";
        public const string FacilityBusy = "Prompt.FacilityBusy";
        public const string FacilityHot = "Prompt.FacilityHot";
        public const string FacilityWash = "Prompt.FacilityWash";
        public const string FacilityUse = "Prompt.FacilityUse";
        public const string NeedsWash = "Prompt.NeedsWash";
        public const string BagRetrieve = "Prompt.BagRetrieve";
        public const string BagBurn = "Prompt.BagBurn";
        public const string BoxLooting = "Prompt.BoxLooting";
        public const string BoxOpen = "Prompt.BoxOpen";
        public const string BoxGridHint = "Prompt.BoxGridHint";
        public const string Receive = "Prompt.Receive";
        public const string Give = "Prompt.Give";
        public const string Swap = "Prompt.Swap";
    }

    public static class Facility
    {
        public const string Coffee = "Facility.Coffee";
        public const string Oven = "Facility.Oven";
        public const string Sink = "Facility.Sink";
        public const string Beans = "Facility.Beans";
        public const string Bread = "Facility.Bread";
    }

    public static class Carry
    {
        public const string Unknown = "Carry.Unknown";
        public const string Burnt = "Carry.Burnt";
        public const string DirtyPlate = "Carry.DirtyPlate";
        public const string DirtyCup = "Carry.DirtyCup";
        public const string CleanPlate = "Carry.CleanPlate";
        public const string CleanCup = "Carry.CleanCup";
        public const string Empty = "Carry.Empty";
        public const string Assembled = "Carry.Assembled";
    }

    public static class Lobby
    {
        public const string RoomFull = "Lobby.RoomFull";
        public const string RoomName = "Lobby.RoomName";
        public const string LeaveBeforeSwitch = "Lobby.LeaveBeforeSwitch";
        public const string ListLoading = "Lobby.ListLoading";
        public const string NoRooms = "Lobby.NoRooms";
        public const string Creating = "Lobby.Creating";
        public const string HostUnknown = "Lobby.HostUnknown";
        public const string Joining = "Lobby.Joining";
        public const string HostOnlyStart = "Lobby.HostOnlyStart";
        public const string CheckReadyAndTeams = "Lobby.CheckReadyAndTeams";
        public const string DataNotLoaded = "Lobby.DataNotLoaded";
        public const string SceneLoadFailed = "Lobby.SceneLoadFailed";
        public const string NoNetworkManager = "Lobby.NoNetworkManager";
        public const string NoTransport = "Lobby.NoTransport";
        public const string HostStartFailed = "Lobby.HostStartFailed";
        public const string ClientStartFailed = "Lobby.ClientStartFailed";
        public const string UnknownFailure = "Lobby.UnknownFailure";
        public const string NotReady = "Lobby.NotReady";
        public const string DataMismatch = "Lobby.DataMismatch";
        public const string HostDataNotLoaded = "Lobby.HostDataNotLoaded";
        public const string EditorName = "Lobby.EditorName";
        public const string LocalFolderCreateFailed = "Lobby.LocalFolderCreateFailed";
        public const string LocalWriteFailed = "Lobby.LocalWriteFailed";
        public const string LocalReadFailed = "Lobby.LocalReadFailed";
        public const string RoomGone = "Lobby.RoomGone";
        public const string RoomFullNow = "Lobby.RoomFullNow";
        public const string SteamConnectFailed = "Lobby.SteamConnectFailed";
        public const string StartSignalNotHost = "Lobby.StartSignalNotHost";
        public const string SteamNotReady = "Lobby.SteamNotReady";
        public const string ListFailed = "Lobby.ListFailed";
        public const string CreateFailedWith = "Lobby.CreateFailedWith";
        public const string CreateFailed = "Lobby.CreateFailed";
        public const string ListStale = "Lobby.ListStale";
        public const string JoinFailedWith = "Lobby.JoinFailedWith";
    }

    public static class Names
    {
        public const string TeamPending = "Names.TeamPending";
        public const string Team = "Names.Team";
        public const string GemItem = "Names.GemItem";
    }
}
