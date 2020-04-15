﻿using MooseSoft.Azure.ServiceBus.Abstractions;
using MooseSoft.Azure.ServiceBus.Abstractions.Builders;
using MooseSoft.Azure.ServiceBus.FailurePolicy;
using System;

namespace MooseSoft.Azure.ServiceBus.Builders
{
    internal class MessageContextProcessorBuilder : BuilderBase<IMessageContextProcessorBuilder>, IMessageContextProcessorBuilder
    {
        public override IMessageContextProcessorBuilder WithBackOffDelayStrategy<TStrategy>(TStrategy backOffDelayStrategy)
        {
            BuilderState.BackOffDelayStrategy = backOffDelayStrategy;
            return this;
        }

        public override IMessageContextProcessorBuilder WithAbandonMessageFailurePolicy()
        {
            BuilderState.FailurePolicy = new AbandonMessageFailurePolicy();
            return this;
        }

        public override IMessageContextProcessorBuilder WithFailurePolicy<TFailurePolicy>(TFailurePolicy failurePolicy)
        {
            BuilderState.FailurePolicy = failurePolicy;
            return this;
        }

        public IMessageContextProcessor Build(Func<Exception, bool> shouldCompleteOn = null) => 
            new MessageContextProcessor(
                BuilderState.MessageProcessor, 
                BuilderState.FailurePolicy ??  CreateFailurePolicy(), 
                shouldCompleteOn);
    }
}