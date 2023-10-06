using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Telekinesis", "redBDGR/RFC1920", "2.0.11")]
    [Description("Control objects with your mind!")]
    class Telekinesis : RustPlugin
    {
        ConfigData configData;
        private static Telekinesis plugin;
        private const string permissionNameADMIN = "telekinesis.admin";
        private const string permissionNameRESTRICTED = "telekinesis.restricted";

        private Dictionary<string, BaseEntity> grabList = new Dictionary<string, BaseEntity>();
        private Dictionary<string, UndoInfo> undoDic = new Dictionary<string, UndoInfo>();

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        private class UndoInfo
        {
            public Vector3 pos;
            public Quaternion rot;
            public BaseEntity entity;
        }

        private void Init()
        {
            plugin = this;
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameRESTRICTED, this);
            LoadConfigVariables();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You are not allowed to use this command!",
                ["Grab tool start"] = "The telekinesis tool has been enabled",
                ["Grab tool end"] = "The telekinesis tool has been disabled",
                ["Invalid entity"] = "No valid entity was found",
                ["Building Blocked"] = "You are not allowed to use this tool if you are building blocked",
                ["No Undo Found"] = "No undo data was found!",
                ["Undo Success"] = "Your last telekinesis movement was undone",
                ["TLS Mode Changed"] = "Current mode: {0}",
            }, this);
        }

        #region config
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        public class ConfigData
        {
            public Settings Settings;
            public VersionNumber Version { get; internal set; }
        }

        public class Settings
        {
            [JsonProperty(PropertyName = "Restricted max distance")]
            public float maxDist = 3f;

            [JsonProperty(PropertyName = "Auto disable length")]
            public float autoDisableLength = 60f;

            [JsonProperty(PropertyName = "Restricted Cannot Use If Building Blocked")]
            public bool restrictedBuildingAuthOnly = true;

            [JsonProperty(PropertyName = "Restricted OwnerID Only")]
            public bool restrictedOwnerIdOnly;

            [JsonProperty(PropertyName = "Restricted Grab Distance")]
            public float restrictedGrabDistance = 20f;

            [JsonProperty(PropertyName = "Restricted Cannot Move Players (Sleeping or Awake)")]
            public bool restrictedCanMoveBasePlayers;
        }
        #endregion

        private void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity ent = entity as BaseEntity;
            if (ent == null) return;
            TelekinesisComponent tls = ent.GetComponent<TelekinesisComponent>();
            if (!tls) return;
            tls.DestroyThis();
            /*
            string x = "0";
            foreach (var entry in grabList)
                if (ent = entry.Value)
                    x = entry.Key;
            if (x != "0")
            {
                grabList.Remove(x);
                undoDic.Remove(x);
            }
            */
        }

        [ChatCommand("tls")]
        private void GrabCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN) && !permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED))
            {
                Message(player.IPlayer, "No Permission");
                return;
            }
            if (configData.Settings.restrictedBuildingAuthOnly && permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED) && !player.CanBuild())
            {
                Message(player.IPlayer, "Building Blocked");
                return;
            }
            if (args.Length == 1 && args[0] == "undo")
            {
                if (!undoDic.ContainsKey(player.UserIDString))
                {
                    Message(player.IPlayer, "No Undo Found");
                    return;
                }
                if (!undoDic[player.UserIDString].entity.IsValid()) return;

                undoDic[player.UserIDString].entity.GetComponent<TelekinesisComponent>().DestroyThis();
                undoDic[player.UserIDString].entity.transform.position = undoDic[player.UserIDString].pos;
                undoDic[player.UserIDString].entity.transform.rotation = undoDic[player.UserIDString].rot;
                undoDic[player.UserIDString].entity.SendNetworkUpdate();
                Message(player.IPlayer, "Undo Success");
                undoDic.Remove(player.UserIDString);
                return;
            }
            if (grabList.ContainsKey(player.UserIDString))
            {
                BaseEntity ent = grabList[player.UserIDString];
                TelekinesisComponent grab = ent.GetComponent<TelekinesisComponent>();
                if (grab) grab.DestroyThis();
                grabList.Remove(player.UserIDString);
                return;
            }
            BaseEntity grabEnt = GrabEntity(player);
            if (grabEnt == null)
            {
                Message(player.IPlayer, "Invalid entity");
                return;
            }
            RemoveActiveItem(player);
            Message(player.IPlayer, "Grab tool start");
        }

        // Active item removal code courtesy of Fujikura
        private void RemoveActiveItem(BasePlayer player)
        {
            foreach (Item item in player.inventory.containerBelt.itemList.Where(x => x.IsValid() && x.GetHeldEntity()).ToList())
            {
                int slot = item.position;
                item.RemoveFromContainer();
                item.MarkDirty();
                timer.Once(0.15f, () =>
                {
                    if (item == null) return;
                    item.MoveToContainer(player.inventory.containerBelt, slot);
                    item.MarkDirty();
                });
            }
        }

        private BaseEntity GrabEntity(BasePlayer player)
        {
            BaseEntity ent = FindEntity(player);
            if (ent == null) return null;

            if (PlayerIsRestricted(player))
            {
                if (configData.Settings.restrictedOwnerIdOnly && ent.OwnerID != player.userID) // Target object ID restriction
                {
                    return null;
                }
                if (Vector3.Distance(ent.transform.position, player.transform.position) >= configData.Settings.restrictedGrabDistance) // Distance restriction
                {
                    return null;
                }
                if (!configData.Settings.restrictedCanMoveBasePlayers && ent.GetComponent<BasePlayer>() != null)
                {
                    return null;
                }
            }
            TelekinesisComponent grab = ent.gameObject.AddComponent<TelekinesisComponent>();
            grab.originPlayer = player;
            undoDic[player.UserIDString] = new UndoInfo { pos = ent.transform.position, rot = ent.transform.rotation, entity = ent };
            grabList.Add(player.UserIDString, ent);
            timer.Once(configData.Settings.autoDisableLength, () =>
            {
                if (grab) grab.DestroyThis();
            });
            return ent;
        }

        private bool PlayerIsRestricted(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED);
        }

        private static BaseEntity FindEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) return null;
            return hit.GetEntity();
        }

        private class TelekinesisComponent : MonoBehaviour
        {
            public BasePlayer originPlayer;
            private BaseEntity target;
            private StabilityEntity stab;
            private float entDis = 2f;
            private float vertOffset = 1f;
            private float maxDis = plugin.configData.Settings.maxDist;
            private bool isRestricted;
            private float nextTime1;
            private float nextTime2;
            private float nextTime3;
            private float nextTime4;
            private string mode = "distance";

            private void Awake()
            {
                nextTime1 = Time.time + 0.5f;
                nextTime2 = Time.time + 0.5f;
                nextTime3 = Time.time + 0.5f;
                nextTime4 = Time.time + 0.5f;
                target = gameObject.GetComponent<BaseEntity>();
                stab = target?.GetComponent<StabilityEntity>();

                plugin.NextTick(() =>
                {
                    if (!originPlayer) return;
                    if (plugin.permission.UserHasPermission(originPlayer.UserIDString, permissionNameRESTRICTED))
                    {
                        isRestricted = true;
                    }
                });
            }

            private void Update()
            {
                if (originPlayer == null) return;
                if (isRestricted && !originPlayer.CanBuild())
                {
                    DestroyThis();
                    return;
                }
                if (originPlayer.serverInput.IsDown(BUTTON.JUMP))
                {
                    DestroyThis();
                    return;
                }
                if (originPlayer.serverInput.IsDown(BUTTON.SPRINT) && Time.time > nextTime1)
                {
                    switch (mode)
                    {
                        case "vertical offset":
                            mode = "rotate (horizontal2)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (horizontal2)":
                            mode = "vertical snap";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (vertical snap)":
                            mode = "rotate (veritical)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (vertical)":
                            mode = "rotate (horizontal snap)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (horizontal snap)":
                            mode = "rotate (horizontal)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (horizontal)":
                            mode = "rotate (distance)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "distance":
                            mode = "vertical offset";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        default:
                            mode = "distance";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                    }
                    nextTime1 = Time.time + 0.5f;
                }
                if (originPlayer.serverInput.IsDown(BUTTON.RELOAD) && Time.time > nextTime2)
                {
                    switch (mode)
                    {
                        case "distance":
                            mode = "rotate (horizontal)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (horizontal)":
                            mode = "rotate (horizontal snap)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (horizontal snap)":
                            mode = "rotate (vertical)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (vertical)":
                            mode = "rotate (vertical snap)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (vertical snap)":
                            mode = "rotate (horizontal2)";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "rotate (horizontal2)":
                            mode = "vertical offset";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        case "vertical offset":
                            mode = "distance";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                        default:
                            mode = "distance";
                            plugin.Message(originPlayer.IPlayer, "TLS Mode Changed", mode);
                            break;
                    }
                    nextTime2 = Time.time + 0.5f;
                }
                if (originPlayer.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY) && mode.Contains("snap"))
                {
                    if (Time.time > nextTime3)
                    {
                        switch (mode)
                        {
                            case "rotate (horizontal snap)":
                                gameObject.transform.Rotate(0, +45f, 0);
                                break;
                            case "rotate (vertical snap)":
                                gameObject.transform.Rotate(0, 0, -45f);
                                break;
                        }
                        nextTime3 = Time.time + 0.25f;
                    }
                }
                else if (originPlayer.serverInput.IsDown(BUTTON.FIRE_PRIMARY) && !mode.Contains("snap"))
                {
                    switch (mode)
                    {
                        case "distance":
                            if (isRestricted)
                            {
                                if (entDis <= maxDis)
                                {
                                    entDis += 0.01f;
                                }
                            }
                            else
                            {
                                entDis += 0.01f;
                            }
                            break;
                        case "rotate (horizontal)":
                            gameObject.transform.Rotate(0, +0.5f, 0);
                            break;
                        case "rotate (vertical)":
                            gameObject.transform.Rotate(0, 0, -0.5f);
                            break;
                        case "rotate (horizontal2)":
                            gameObject.transform.Rotate(+0.5f, 0, 0);
                            break;
                        case "vertical offset":
                            vertOffset += 0.02f;
                            break;
                    }
                }
                else if (originPlayer.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY) && mode.Contains("snap"))
                {
                    if (Time.time > nextTime4)
                    {
                        switch (mode)
                        {
                            case "rotate (horizontal snap)":
                                gameObject.transform.Rotate(0, -45f, 0);
                                break;
                            case "rotate (vertical snap)":
                                gameObject.transform.Rotate(0, 0, +45f);
                                break;
                        }
                        nextTime4 = Time.time + 0.25f;
                    }
                }
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_SECONDARY) && !mode.Contains("snap"))
                {
                    switch (mode)
                    {
                        case "distance":
                            entDis -= 0.01f;
                            break;
                        case "rotate (horizontal)":
                            gameObject.transform.Rotate(0, -0.5f, 0);
                            break;
                        case "rotate (vertical)":
                            gameObject.transform.Rotate(0, 0, +0.5f);
                            break;
                        case "rotate (horizontal2)":
                            gameObject.transform.Rotate(-0.5f, 0, 0);
                            break;
                        case "vertical offset":
                            vertOffset -= 0.02f;
                            break;
                    }
                }
                //if (!rotateMode)
                //gameObject.transform.LookAt(originPlayer.transform);
                //else
                //gameObject.transform.Rotate(gameObject.transform.rotation.x, roty, gameObject.transform.rotation.z);
                target.transform.position = Vector3.Lerp(target.transform.position, originPlayer.transform.position + originPlayer.eyes.HeadRay().direction * entDis + new Vector3(0, vertOffset, 0), UnityEngine.Time.deltaTime * 15f);
                if (stab?.grounded == false) stab.grounded = true;

                target.transform.hasChanged = true;
                target.UpdateNetworkGroup();
                target.SendNetworkUpdateImmediate();
            }

            public void DestroyThis()
            {
                //if (plugin.undoDic.ContainsKey(originPlayer.UserIDString))
                //    plugin.undoDic.Remove(originPlayer.UserIDString);
                if (plugin.grabList.ContainsKey(originPlayer.UserIDString))
                {
                    plugin.grabList.Remove(originPlayer.UserIDString);
                }
                plugin.Message(originPlayer.IPlayer, "Grab tool end");
                Destroy(this);
            }
        }
    }
}
