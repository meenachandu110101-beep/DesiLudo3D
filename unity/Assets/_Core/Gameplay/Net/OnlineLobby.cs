using System.Collections;
using System.Collections.Generic;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Gray-box online lobby (M5): HOST (players 2–4, room code,
    /// live guest count, START — vacant seats fill with tiered bots) /
    /// JOIN (discovered LAN rooms + manual IP entry for direct play).
    /// Friends-only by construction: private code rooms, no matchmaking.
    /// Builds the online scene via the GameBootstrap hooks.</summary>
    public class OnlineLobby : MonoBehaviour
    {
        GameBootstrap _bootstrap;
        LanTransport _net;
        GameObject _panel;
        Text _status;
        float _nextRefresh;

        public void Open(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            if (_net == null) _net = gameObject.AddComponent<LanTransport>();
            BuildPanel();
            RefreshReconnectButton();   // M6: rejoin offered whenever valid
            _net.StartDiscovery();
            _panel.SetActive(true);
        }

        void RefreshReconnectButton()
        {
            if (_reconnectButton != null) Destroy(_reconnectButton.gameObject);
            _reconnectButton = null;
            string persisted = PlayerPrefs.GetString(PersistKey, "");
            if (persisted.Length == 0) return;
            string[] parts = persisted.Split('|');
            if (parts.Length != 4) return;
            _reconnectButton = MakeButton(_panel.transform, "Reconnect",
                "REJOIN MATCH (" + parts[1] + " @ " + parts[0] + ")",
                new Color(0.55f, 0.35f, 0.6f), 30);
            Bottom(_reconnectButton.GetComponent<RectTransform>(),
                new Vector2(0f, 200f), new Vector2(700f, 100f));
            _reconnectButton.onClick.AddListener(() => StartCoroutine(
                ReconnectFlow(parts[0], parts[1],
                    (Seat)System.Enum.Parse(typeof(Seat), parts[2], true),
                    parts[3])));
        }

        void BuildPanel()
        {
            if (_panel != null) return;
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _panel = new GameObject("OnlineLobby", typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.96f);

            var title = MakeText(_panel.transform, "Title", "ONLINE — friends only",
                52, FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            Top(title.rectTransform, new Vector2(0f, -80f), new Vector2(1040f, 90f));

            _status = MakeText(_panel.transform, "Status", "", 30, FontStyle.Normal,
                new Color(1f, 0.9f, 0.7f));
            Top(_status.rectTransform, new Vector2(0f, -150f), new Vector2(1040f, 70f));

            // ---------------- host side ----------------
            var hostLabel = MakeText(_panel.transform, "HostLabel", "HOST A GAME",
                40, FontStyle.Bold, Color.white);
            Place(hostLabel.rectTransform, -270f, 260f, 460f, 70f);

            _playersButton = MakeButton(_panel.transform, "Players", "PLAYERS: 2",
                new Color(0.25f, 0.35f, 0.55f), 32);
            Place(_playersButton.GetComponent<RectTransform>(), -270f, 170f, 460f, 100f);
            _playersButton.onClick.AddListener(CyclePlayers);

            _hostButton = MakeButton(_panel.transform, "HostBtn", "CREATE ROOM",
                new Color(0.2f, 0.5f, 0.3f), 34);
            Place(_hostButton.GetComponent<RectTransform>(), -270f, 50f, 460f, 110f);
            _hostButton.onClick.AddListener(StartHosting);

            // ---------------- join side ----------------
            var joinLabel = MakeText(_panel.transform, "JoinLabel", "JOIN A GAME",
                40, FontStyle.Bold, Color.white);
            Place(joinLabel.rectTransform, 270f, 260f, 460f, 70f);

            _roomList = new GameObject("RoomList", typeof(RectTransform));
            _roomList.transform.SetParent(_panel.transform, false);
            _roomList.GetComponent<RectTransform>().Stretch();

            _manualField = MakeInputField(_panel.transform, "ManualField",
                "host-ip  (e.g. 192.168.1.42)");
            Place(_manualField.GetComponent<RectTransform>(), 270f, 170f, 460f, 100f);

            _joinButton = MakeButton(_panel.transform, "JoinBtn", "CONNECT",
                new Color(0.2f, 0.5f, 0.3f), 34);
            Place(_joinButton.GetComponent<RectTransform>(), 270f, 50f, 460f, 110f);
            _joinButton.onClick.AddListener(JoinManual);

            var close = MakeButton(_panel.transform, "Close", UiStrings.Get("close"),
                new Color(0.6f, 0.3f, 0.1f), 40);
            Bottom(close.GetComponent<RectTransform>(), new Vector2(0f, 60f), new Vector2(360f, 110f));
            close.onClick.AddListener(() => { _net.StopDiscovery(); _panel.SetActive(false); });
        }

        public const string PersistKey = "desiludo.online.session";

        IEnumerator ReconnectFlow(string ip, string code, Seat seat, string keyHex)
        {
            var guest = new NetGuestSession("Player");
            var transport = _net.StartGuestSide();
            if (!transport.Connect(ip))
            {
                _status.text = "Host not reachable — is the host device still in the match?";
                yield break;
            }
            _status.text = "Rejoining room " + code + "…";
            guest.Send = transport.Send;
            guest.RejoinWithProof(code, seat, keyHex);
            float waited = 0f;
            while ((guest.SeatIndex < 0 || guest.Config == null) && waited < 5f)
            {
                transport.Pump(guest.OnLine);
                if (guest.Errors.Count > 0) break;
                waited += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            transport.Pump(guest.OnLine);
            if (guest.SeatIndex < 0 || guest.Config == null)
            {
                _status.text = guest.Errors.Count > 0
                    ? guest.Errors[0] : "Rejoin failed (no answer).";
                transport.Disconnect();
                yield break;
            }
            guest.Resume();   // full log replay → shadow state converges
            _net.StopDiscovery();
            _panel.SetActive(false);
            _bootstrap.EnterOnlineGuestMode(guest, transport);
        }

        Button _reconnectButton;

        Button _playersButton;
        Button _hostButton;
        Button _joinButton;
        GameObject _roomList;
        InputField _manualField;
        int _players = 2;
        LanTransport.HostSide _hostSide;
        NetHostSession _hostSession;
        string _roomCode;

        void CyclePlayers()
        {
            _players = _players >= 4 ? 2 : _players + 1;
            _playersButton.GetComponentInChildren<Text>().text = "PLAYERS: " + _players;
        }

        // ------------------------------------------------------------ hosting

        void StartHosting()
        {
            _roomCode = NetProtocol.MakeRoomCode(
                new NetProtocol.SplitMix64((ulong)System.DateTime.UtcNow.Ticks));
            _hostSession = new NetHostSession(_roomCode, "Host", _players, blockades: true);
            _hostSession.HostCommit();
            _hostSide = _net.StartHostSide(_hostSession);
            _net.StartBeacon(_roomCode, "DesiLudo3D", 1, _players);
            _hostSession.MatchStartedEvent += () =>
            {
                _net.StopBeacon();
                _panel.SetActive(false);
                _bootstrap.EnterOnlineHostMode(_hostSession, _hostSide, seed: 12345);
            };
            _status.text = "ROOM " + _roomCode + " — share the code. Waiting for friends…";
            _hostButton.GetComponentInChildren<Text>().text = "START (bots fill empty seats)";
            _hostButton.onClick.RemoveListener(StartHosting);
            _hostButton.onClick.AddListener(StartMatchWithBots);
            StartCoroutine(UpdateHostStatus());
        }

        void StartMatchWithBots()
        {
            // vacant seats become host-side bots (each commits a seed too)
            foreach (Seat seat in _hostSession.Seats)
            {
                if (!_hostSession.IsSeatClaimed(seat)) _hostSession.BotCommit(seat);
            }
            _status.text = "Starting… (waiting for reveals)";
        }

        IEnumerator UpdateHostStatus()
        {
            while (_panel != null && _panel.activeSelf && _hostSession != null)
            {
                if (!_hostSession.MatchStarted)
                    _status.text = "ROOM " + _roomCode + " — friends joined: "
                                 + _hostSession.GuestCount + ". Waiting / press START.";
                yield return new WaitForSeconds(1f);
            }
        }

        // ------------------------------------------------------------ joining

        /// <summary>Manual entry format "ip:CODE" (e.g. 192.168.1.42:K7QP2M)
        /// — works for LAN and direct/internet play (M6).</summary>
        void JoinManual()
        {
            string entry = _manualField.text.Trim().ToUpper();
            if (entry.Length == 0) return;
            string ip = entry;
            string code = "";
            int colon = entry.LastIndexOf(':');
            if (colon > 0)
            {
                ip = entry.Substring(0, colon);
                code = entry.Substring(colon + 1);
            }
            if (code.Length == 0)
            {
                _status.text = "Enter host as  ip:ROOMCODE  (code is shown on the host screen)";
                return;
            }
            StartCoroutine(JoinFlow(ip, code));
        }

        IEnumerator JoinFlow(string ip, string code)
        {
            var guest = new NetGuestSession();
            var transport = _net.StartGuestSide();
            if (!transport.Connect(ip))
            {
                _status.text = "Could not reach " + ip;
                yield break;
            }
            _status.text = "Connected to " + ip + " — joining room " + code + "…";
            guest.Send = transport.Send;
            guest.Join(code, "Guest");
            // welcome/bad-code reply arrives via the socket thread; pump it
            float waited = 0f;
            while (guest.SeatIndex < 0 && waited < 4f)
            {
                transport.Pump(guest.OnLine);
                if (guest.Errors.Count > 0) break;
                waited += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            transport.Pump(guest.OnLine);
            if (guest.SeatIndex < 0)
            {
                _status.text = guest.Errors.Count > 0
                    ? guest.Errors[0] : "Join failed (no answer).";
                transport.Disconnect();
                yield break;
            }
            guest.SendCommit(CommitRevealDice.CreateSeed());
            // persist enough to rejoin after an app restart (M6)
            StartCoroutine(PersistWhenSeated(guest, ip));
            _net.StopDiscovery();
            _panel.SetActive(false);
            _bootstrap.EnterOnlineGuestMode(guest, transport);
        }

        IEnumerator PersistWhenSeated(NetGuestSession guest, string ip)
        {
            // the session key arrives in `welcome` (already pumped by now);
            // PersistedSession() returns null until seat + key are known
            float waited = 0f;
            while (waited < 10f)
            {
                string persisted = guest.PersistedSession();
                if (persisted != null)
                {
                    string[] parts = persisted.Split('|');
                    PlayerPrefs.SetString(PersistKey,
                        ip + "|" + parts[0] + "|" + parts[1] + "|" + parts[2]);
                    PlayerPrefs.Save();
                    yield break;
                }
                waited += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }
        }

        IEnumerator RefreshRoomList()
        {
            while (_panel != null && _panel.activeSelf)
            {
                yield return new WaitForSeconds(1.5f);
                RebuildRooms();
            }
        }

        void RebuildRooms()
        {
            if (_roomList == null) return;
            for (int i = _roomList.transform.childCount - 1; i >= 0; i--)
                Destroy(_roomList.transform.GetChild(i).gameObject);

            var rooms = new List<LanTransport.RoomInfo>(_net.Rooms);
            for (int i = 0; i < rooms.Count && i < 5; i++)
            {
                var room = rooms[i];
                var b = MakeButton(_roomList.transform, "Room_" + room.Code,
                    room.Code + "  " + room.Name + " (" + room.Current + "/" + room.Max + ")",
                    new Color(0.25f, 0.35f, 0.55f), 28);
                float y = 60f - i * 100f;
                var rt = b.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(270f, y);
                rt.sizeDelta = new Vector2(460f, 90f);
                string ip = room.Ip;
                string code = room.Code;
                b.onClick.AddListener(() => StartCoroutine(JoinFlow(ip, code)));
            }
        }

        // ------------------------------------------------------------ helpers

        static void Top(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        static void Bottom(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static Text MakeText(Transform parent, string name, string text, int size,
                             FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label,
                                 Color color, int fontSize)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            var text = MakeText(go.transform, "Label", label, fontSize, FontStyle.Bold, Color.white);
            text.rectTransform.Stretch();
            text.raycastTarget = false;
            return button;
        }

        static InputField MakeInputField(Transform parent, string name, string placeholder)
        {
            var go = new GameObject(name, typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var field = go.GetComponent<InputField>();
            var text = MakeText(go.transform, "Text", "", 30, FontStyle.Normal, Color.white);
            text.rectTransform.Stretch();
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            field.textComponent = text;
            field.text = "";
            var ph = MakeText(go.transform, "Placeholder", placeholder, 26,
                FontStyle.Italic, new Color(1f, 1f, 1f, 0.4f));
            ph.rectTransform.Stretch();
            field.placeholder = ph;
            return field;
        }
    }

    /// <summary>LanTransport host/guest side factories (extension).</summary>
    public static class LanTransportExtensions
    {
        public static LanTransport.HostSide StartHostSide(
            this LanTransport net, NetHostSession session)
        {
            var side = new LanTransport.HostSide(net, 3);
            session.Send = side.SendTo;
            session.Broadcast = side.Broadcast;
            return side;
        }

        public static LanTransport.GuestSide StartGuestSide(this LanTransport net)
        {
            return new LanTransport.GuestSide();
        }
    }
}
