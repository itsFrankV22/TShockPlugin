﻿using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static TShockAPI.GetDataHandlers;
using Terraria.ID;

namespace Goodnight
{
    [ApiVersion(2, 1)]
    public class Goodnight : TerrariaPlugin
    {
        #region 变量与插件信息
        public override string Name => "宵禁";
        public override string Author => "Jonesn 羽学";
        public override Version Version => new Version(2, 3, 0);
        public override string Description => "设置服务器无法进入或禁止生成怪物的时段";
        internal static Configuration Config;
        #endregion

        #region 构造注册卸载
        public Goodnight(Main game) : base(game) { }

        public override void Initialize()
        {
            LoadConfig();
            NewProjectile += NewProj!;
            GeneralHooks.ReloadEvent += LoadConfig;
            ServerApi.Hooks.NpcSpawn.Register(this, OnSpawn);
            ServerApi.Hooks.NpcTransform.Register(this, OnTransform);
            ServerApi.Hooks.NpcKilled.Register(this, OnNPCKilled);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            TShockAPI.Commands.ChatCommands.Add(new Command("goodnight.admin", Commands.GnCmd, "gn", "宵禁"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NewProjectile -= NewProj!;
                GeneralHooks.ReloadEvent -= LoadConfig;
                ServerApi.Hooks.NpcSpawn.Deregister(this, OnSpawn);
                ServerApi.Hooks.NpcTransform.Deregister(this, OnTransform);
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNPCKilled);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                TShockAPI.Commands.ChatCommands.Remove(new Command("goodnight.admin", Commands.GnCmd, "gn", "宵禁"));

            }
            base.Dispose(disposing);
        }
        #endregion


        #region 配置文件创建与重读加载方法
        internal static void LoadConfig(ReloadEventArgs args = null!)
        {
            if (!File.Exists(Configuration.FilePath))
            {
                Config = new Configuration();
                Config.Write();
            }
            else
                Config = Configuration.Read();
            if (args != null && args.Player != null)
                args.Player.SendSuccessMessage("[宵禁]重新加载配置完毕。");
        }
        #endregion


        #region 宵禁
        private void OnJoin(JoinEventArgs args)
        {
            if (!Config.Enabled) return;
            var plr = TShock.Players[args.Who];
            if (DateTime.Now.TimeOfDay >= Config.Time.Start && DateTime.Now.TimeOfDay < Config.Time.Stop)
            {
                if (Config.DiscPlayers)
                    if (plr != null && !Config.Exempt(plr.Name))
                        plr.Disconnect($"{Config.JoinMessage} \n禁止游戏时间:{Config.Time.Start}-{Config.Time.Stop}");
            }
        }

        private void NewProj(object sender, NewProjectileEventArgs e)
        {
            if (!Config.Enabled) return;
            if (DateTime.Now.TimeOfDay >= Config.Time.Start && DateTime.Now.TimeOfDay < Config.Time.Stop)
            {
                if (Config.DiscPlayers)
                    if (e.Player != null && !Config.Exempt(e.Player.Name) && !e.Player.HasPermission("goodnight.admin"))
                        e.Player.Disconnect($"{Config.NewProjMessage} \n禁止游戏时间:{Config.Time.Start}-{Config.Time.Stop}");
            }
        }
        #endregion


        #region 禁止召唤怪物
        private void OnSpawn(NpcSpawnEventArgs args)
        {
            int PlayerCount = TShock.Utils.GetActivePlayerCount();
            bool Npcs = Config.Npcs.Contains(Main.npc[args.NpcId].netID);
            bool NpcDie = Config.NpcDie.Contains(Main.npc[args.NpcId].netID);
            bool NoPlr = PlayerCount < Config.MaxPlayers && Config.MaxPlayers > 0;

            if (args.Handled || !Config.Enabled) return;

            else if (DateTime.Now.TimeOfDay >= Config.Time.Start && DateTime.Now.TimeOfDay < Config.Time.Stop)
            {
                if (NoPlr)
                {
                    if (NpcDie)
                    {
                        args.Handled = false;
                        Main.npc[args.NpcId].active = true;
                        TShock.Utils.Broadcast($"允许召唤已击败的怪物为："
                            + string.Join(", ", Config.NpcDie.Select
                            (x => TShock.Utils.GetNPCById(x)?.FullName + "({0})".SFormat(x))),
                            Microsoft.Xna.Framework.Color.AntiqueWhite);
                    }

                   else if (Npcs)
                    {
                        args.Handled = true;
                        Main.npc[args.NpcId].active = false;
                        TShock.Utils.Broadcast($"【宵禁】当前服务器处于维护时间\n" +
                            $"在线人数少于[c/FF3A4B:{Config.MaxPlayers}人]或该怪物[c/338AE1:不允许召唤]\n" +
                            $"禁止召唤怪物时间: " +
                            $"[c/DF95EC:{Config.Time.Start}] — [c/FF9187:{Config.Time.Stop}]", Microsoft.Xna.Framework.Color.AntiqueWhite);
                    }
                }

                else
                {
                    if (Npcs)
                        args.Handled = false;
                    Main.npc[args.NpcId].active = true;
                }
            }
        }

        private void OnTransform(NpcTransformationEventArgs args)
        {
            int PlayerCount = TShock.Utils.GetActivePlayerCount();
            bool NpcDie = Config.NpcDie.Contains(Main.npc[args.NpcId].netID);
            bool Npcs = Config.Npcs.Contains(Main.npc[args.NpcId].netID);
            bool NoPlr = PlayerCount <= Config.MaxPlayers && Config.MaxPlayers > 0;

            if (args.Handled || !Config.Enabled) return;
            else if (DateTime.Now.TimeOfDay >= Config.Time.Start && DateTime.Now.TimeOfDay < Config.Time.Stop)
            {
                if (NoPlr)
                {
                    if (NpcDie)
                    {
                        Main.npc[args.NpcId].active = true;
                    }

                   else if (Npcs)
                    {
                        Main.npc[args.NpcId].active = false;
                    }
                }

                else
                {
                    if (Npcs)
                    Main.npc[args.NpcId].active = true;
                }
            }
        }

        private void OnNPCKilled(NpcKilledEventArgs args)
        {
            int killNpc = args.npc.netID;

            if (killNpc == 398)
            {
                Config.NpcDie.Clear();
                Config.Write();
                TShock.Utils.Broadcast($"玩家已击败了月亮领主，清空已击败进度记录", Microsoft.Xna.Framework.Color.AntiqueWhite);
            }
            else if (Config.Npcs.Contains(killNpc))
            {
                if (!Config.NpcDie.Contains(killNpc))
                {
                    Config.NpcDie.Add(killNpc);
                    Config.Write();
                    TShock.Utils.Broadcast($"NPC: {killNpc} 已被击败并记录可召唤怪物中：\n"
                        + string.Join(", ", Config.NpcDie.Select
                        (x => TShock.Utils.GetNPCById(x)?.FullName + "({0})".SFormat(x))),
                        Microsoft.Xna.Framework.Color.AntiqueWhite);
                }
            }
        }
        #endregion

    }
}