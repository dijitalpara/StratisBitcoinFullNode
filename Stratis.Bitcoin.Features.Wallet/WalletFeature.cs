﻿using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Wallet feature for the full node.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
    /// <seealso cref="Stratis.Bitcoin.Interfaces.INodeStats" />
    public class WalletFeature : FullNodeFeature, INodeStats
    {
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IWalletManager walletManager;
        private readonly Signals.Signals signals;

        private IDisposable blockSubscriberdDisposable;
        private IDisposable transactionSubscriberdDisposable;
        private ConcurrentChain chain;

        /// <summary>
        /// Initializes a new instance of the <see cref="WalletFeature"/> class.
        /// </summary>
        /// <param name="walletSyncManager">The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="signals">The signals responsible for receiving blocks and transactions from the network.</param>
        /// <param name="chain">The chain of blocks.</param>
        public WalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, Signals.Signals signals, ConcurrentChain chain)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.signals = signals;
            this.chain = chain;
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            var walletManager = this.walletManager as WalletManager;

            if (walletManager != null)
            {
                var height = walletManager.LastBlockHeight();
                var block = this.chain.GetBlock(height);
                var hashBlock = block == null ? 0 : block.HashBlock;

                benchLogs.AppendLine("Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                        height.ToString().PadRight(8) +
                                        " Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                        hashBlock);
            }
        }

        /// <inheritdoc />
        public override void Start()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberdDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.walletSyncManager));
            this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletSyncManager));

            this.walletManager.Initialize();
            this.walletSyncManager.Initialize();
        }

        /// <inheritdoc />
        public override void Stop()
        {
            this.blockSubscriberdDisposable.Dispose();
            this.transactionSubscriberdDisposable.Dispose();

            this.walletManager.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<WalletFeature>("wallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<WalletFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                        services.AddSingleton<IWalletTransactionHandler, WalletTransactionHandler>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                        services.AddSingleton<WalletController>();
                        services.AddSingleton<WalletRPCController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
