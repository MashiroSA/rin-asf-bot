﻿// ASF RinBot Plugin by @chitsanfei

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.CustomPlugins.Bot.Rin.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Data;
using ArchiSteamFarm.CustomPlugins.Rin.Api;
using SteamKit2;

namespace ArchiSteamFarm.CustomPlugins.Rin {
	
	[Export(typeof(IPlugin))]
	internal sealed class RinBot : IASF, IBot, IBotCommand2, IBotConnection, IBotFriendRequest, IBotMessage, IBotModules, IBotTradeOffer {
		
		// Plugin Name
		public string Name => nameof(RinBot);

		// Plugin Version
		public Version Version => typeof(RinBot).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

		// Custom Field to Enable/Disable Plugin
		[JsonInclude]
		public bool CustomIsEnabledField { get; private set; } = true;

		// Dictionary to Track User Request Limits
		private readonly Dictionary<ulong, (int count, DateTime lastRequestTime)> UserRequestLimits = new Dictionary<ulong, (int, DateTime)>();

		// ASF Initialization
		public Task OnASFInit(IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			return Task.CompletedTask;
		}
		
		// Plugin Loaded
		public Task OnLoaded() {
			ASF.ArchiLogger.LogGenericWarning(Langs.InitRinLoaded);
			return Task.CompletedTask;
		}

		// Bot Initialization with Modules
		public async Task OnBotInitModules(Steam.Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			bot.ArchiLogger.LogGenericInfo("Pausing this bot as asked from the plugin");
			await bot.Actions.Pause(true).ConfigureAwait(false);
		}

		// Bot Initialization
		public Task OnBotInit(Steam.Bot bot) {
			ASF.ArchiLogger.LogGenericWarning(Langs.InitNotice + Langs.VersionASF);
			ASF.ArchiLogger.LogGenericWarning(Langs.InitProgramUnstable);
			return Task.CompletedTask;
		}
		
		// Bot Logged On Event
		public Task OnBotLoggedOn(Steam.Bot bot) => Task.CompletedTask;
		
		// Bot Command Processing
		public async Task<string?> OnBotCommand(Steam.Bot bot, EAccess access, string message, string[] args, ulong steamId = 0) {
			const int maxRequestsPerMinute = 30;

			// Rate Limiting
			if (UserRequestLimits.TryGetValue(steamId, out var userLimit) && (DateTime.Now - userLimit.lastRequestTime).TotalMinutes < 1) {
				if (userLimit.count >= maxRequestsPerMinute) {
					return Langs.WarningRateLimit;
				}
				UserRequestLimits[steamId] = (userLimit.count + 1, DateTime.Now);
			} else {
				UserRequestLimits[steamId] = (1, DateTime.Now);
			}

			// Helper Functions to Get URL or Error Message
			Func<Task<string?>, string, Task<string?>> getUrlOrErrorMessage = async (getUrlTask, errorMessage) => {
				string? url = await getUrlTask.ConfigureAwait(false);
				return !string.IsNullOrEmpty(url) ? url : errorMessage;
			};

			Func<Task<Uri?>, string, Task<string?>> getUriOrErrorMessage = async (getUriTask, errorMessage) => {
				Uri? uri = await getUriTask.ConfigureAwait(false);
				return uri != null ? uri.ToString() : errorMessage;
			};

			// Command Handling
			switch (args[0].ToUpperInvariant()) {
				case "SETU":
					return await getUrlOrErrorMessage(SetuAPI.GetRandomSetuUrl(bot.ArchiWebHandler.WebBrowser), Langs.WarningSetuLost).ConfigureAwait(false);
				case "R18" when access >= EAccess.Operator:
					return await getUrlOrErrorMessage(SetuAPI.GetRandomSetuR18Url(bot.ArchiWebHandler.WebBrowser), Langs.WarningSetuLost).ConfigureAwait(false);
				case "R18":
					if (Utils.CheckFileExists()) {
						return await getUrlOrErrorMessage(SetuAPI.GetRandomSetuR18Url(bot.ArchiWebHandler.WebBrowser), Langs.WarningSetuLost).ConfigureAwait(false);
					}
					return Langs.WarningNoPermission;
				case "ANIME":
					return await getUrlOrErrorMessage(AnimePicAPI.GetRandomAnimePic(bot.ArchiWebHandler.WebBrowser), Langs.WarningAnimePicLost).ConfigureAwait(false);
				case "CAT":
					return await getUriOrErrorMessage(CatAPI.GetRandomCatUrl(bot.ArchiWebHandler.WebBrowser), Langs.WarningCatLost).ConfigureAwait(false);
				case "H":
					return Langs.HelpMenu;
				case "ABT":
					return Langs.About;
				default:
					return Langs.WarningNoCommand;
			}
		}

		// Bot Disconnected Event
		public Task OnBotDisconnected(Steam.Bot bot, EResult reason) {
			ASF.ArchiLogger.LogGenericWarning(Langs.WarningBotDisconnected);
			return Task.CompletedTask;
		}

		// Bot Message Processing
		public Task<string?> OnBotMessage(Steam.Bot bot, ulong steamId, string message) {
			if (Steam.Bot.BotsReadOnly == null) {
				throw new InvalidOperationException(nameof(Steam.Bot.BotsReadOnly));
			}

			// Check if Message Contains a Web Link
			if (Steam.Bot.BotsReadOnly.Values.Any(existingBot => existingBot.SteamID == steamId)) {
				return Task.FromResult<string?>(null);
			}

			if (message.Contains('.', StringComparison.OrdinalIgnoreCase) && message.Length > 4) {
				HashSet<string> webDomainList = new HashSet<string> {
					"http", ".top", ".com", ".cat", ".mba", ".cn", ".xyz", ".cc", ".co", ".icu", ".uk", ".us", ".ca", ".sh", ".sk", ".st", ".au",
					".net", ".org", ".info", ".biz", ".name", ".museum", ".edu", ".gov", ".int", ".eu", ".aero", ".pro", ".travel", ".tel", ".jobs", ".coop", ".mobi", ".asia", ".post", ".xxx", ".global",
					".de", ".fr", ".it", ".es", ".pl", ".ru", ".br", ".in", ".jp", ".kr", ".vn", ".mx", ".ar", ".za", ".ch", ".se", ".no", ".fi", ".dk", ".be", ".at", ".ir", ".il", ".pt", ".nz", ".hk", ".my", ".sg", ".th", ".ph", ".id"
				};
				if (webDomainList.Any(s => message.Contains(s, StringComparison.OrdinalIgnoreCase))) {
					string reply = $"/pre 🤔 -> SteamUser64ID:{steamId}\n{Langs.WarningWebLink}";
					return Task.FromResult((string?)reply);
				}
			}

			return Task.FromResult((string?)"");
		}

		// Bot Trade Offer Event
		public Task<bool> OnBotTradeOffer(Steam.Bot bot, TradeOffer tradeOffer) => Task.FromResult(false);

		// Bot Friend Request Event
		public Task<bool> OnBotFriendRequest(Steam.Bot bot, ulong steamId) => Task.FromResult(false);

		// Bot Destroy Event
		public Task OnBotDestroy(Steam.Bot bot) => Task.CompletedTask;

	}
}
