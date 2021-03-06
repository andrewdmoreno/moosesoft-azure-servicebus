﻿using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using MooseSoft.Azure.ServiceBus.Abstractions;
using MooseSoft.Azure.ServiceBus.BackOffDelayStrategy;
using MooseSoft.Azure.ServiceBus.FailurePolicy;
using System;
using System.Threading.Tasks;

namespace MooseSoft.Azure.ServiceBus.MessagePump
{
    internal class MessagePumpBuilder 
        : IFailurePolicyHolder, IBackDelayStrategyHolder, IMessagePumpBuilder, IMessageProcessorHolder
    {
        internal MessagePumpBuilderState BuilderState { get; }

        public MessagePumpBuilder(IMessageReceiver messageReceiver)
        {
            BuilderState = new MessagePumpBuilderState
            {
                MessageReceiver = messageReceiver
            };
        }

        #region IFailurePolicyHolder Members
        public IBackDelayStrategyHolder WithCloneMessageFailurePolicy(Func<Exception, bool> canHandle = null)
        {
            SetFailurePolicyInfo(typeof(CloneMessageFailurePolicy), canHandle);
            return this;
        }

        public IBackDelayStrategyHolder WithDeferMessageFailurePolicy(Func<Exception, bool> canHandle = null)
        {
            SetFailurePolicyInfo(typeof(DeferMessageFailurePolicy), canHandle);
            return this;
        }

        public IMessagePumpBuilder WithAbandonMessageFailurePolicy()
        {
            WithFailurePolicy(new AbandonMessageFailurePolicy());
            return this;
        }


        public IMessagePumpBuilder WithFailurePolicy<T>(T failurePolicy) where T : IFailurePolicy
        {
            BuilderState.FailurePolicy = failurePolicy;
            return this;
        } 
        #endregion

        #region IBackOffDelayStrategyHolder Members
        public IMessagePumpBuilder WithBackOffDelayStrategy<T>(T backOffDelayStrategy) where T : IBackOffDelayStrategy
        {
            BuilderState.BackOffDelayStrategy = backOffDelayStrategy;
            return this;
        }

        public IMessagePumpBuilder WithBackOffDelayStrategy<T>() where T : IBackOffDelayStrategy, new()
        {
            return WithBackOffDelayStrategy(new T());
        }

        public IMessagePumpBuilder WithExponentialBackOffDelayStrategy() =>
            WithBackOffDelayStrategy(ExponentialBackOffDelayStrategy.Default);

        public IMessagePumpBuilder WithExponentialBackOffDelayStrategy(TimeSpan maxDelay) =>
            WithBackOffDelayStrategy(new ExponentialBackOffDelayStrategy(maxDelay));

        public IMessagePumpBuilder WithConstantBackOffDelayStrategy() =>
            WithBackOffDelayStrategy(ConstantBackOffDelayStrategy.Default);

        public IMessagePumpBuilder WithConstantBackOffDelayStrategy(TimeSpan delayTime) =>
            WithBackOffDelayStrategy(new ConstantBackOffDelayStrategy(delayTime));

        public IMessagePumpBuilder WithLinearBackOffDelayStrategy() =>
            WithBackOffDelayStrategy(LinearBackOffDelayStrategy.Default);

        public IMessagePumpBuilder WithLinearBackOffDelayStrategy(TimeSpan delayTime) =>
            WithBackOffDelayStrategy(new LinearBackOffDelayStrategy(delayTime));

        public IMessagePumpBuilder WithZeroBackOffDelayStrategy() =>
            WithBackOffDelayStrategy(new ZeroBackOffDelayStrategy());
        #endregion

        #region IMessagePumpBuilder Members
        public IMessageReceiver BuildMessagePump(
            Func<ExceptionReceivedEventArgs, Task> exceptionHandler,
            int maxConcurrentCalls = 10,
            Func<Exception, bool> shouldCompleteOnException = null)
        {
            var options = new MessagePumpBuilderOptions(exceptionHandler)
            {
                MaxConcurrentCalls = maxConcurrentCalls
            };

            return BuildMessagePump(options);
        }

        public IMessageReceiver BuildMessagePump(MessagePumpBuilderOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var contextProcessor = CreateMessageContextProcessor(options.ShouldCompleteOnException);

            BuilderState.MessageReceiver.RegisterMessageHandler(
                (message, token) => contextProcessor.ProcessMessageContextAsync(
                    new MessageContext(message, BuilderState.MessageReceiver), token),
                options);

            return BuilderState.MessageReceiver;
        }
        #endregion

        #region IMessageProcessorHolder Members
        public IFailurePolicyHolder WithMessageProcessor<T>(T messageProcessor)
            where T : IMessageProcessor
        {
            BuilderState.MessageProcessor = messageProcessor;
            return this;
        }

        public IFailurePolicyHolder WithMessageProcessor<T>() where T : IMessageProcessor, new()
        {
            return WithMessageProcessor(new T());
        }
        #endregion

        private IMessageContextProcessor CreateMessageContextProcessor(Func<Exception, bool> shouldCompleteOnException = null) =>
            new MessageContextProcessor(
                BuilderState.MessageProcessor,
                BuilderState.FailurePolicy ?? CreateFailurePolicy(),
                shouldCompleteOnException);

        private IFailurePolicy CreateFailurePolicy()
        {
            return BuilderState.FailurePolicyType == typeof(CloneMessageFailurePolicy)
                ? new CloneMessageFailurePolicy(BuilderState.CanHandle, BuilderState.BackOffDelayStrategy)
                : (IFailurePolicy)new DeferMessageFailurePolicy(BuilderState.CanHandle, BuilderState.BackOffDelayStrategy);
        }

        internal static bool DefaultCanHandle(Exception exception) => true;

        private void SetFailurePolicyInfo(Type failurePolicyType, Func<Exception, bool> canHandle)
        {
            BuilderState.FailurePolicyType = failurePolicyType;
            BuilderState.CanHandle = canHandle ?? DefaultCanHandle;
        }
    }
}