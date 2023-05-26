﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Vortice;

public interface IGraphicsDevice : IDisposable
{
    bool DrawFrame(Action<int, int> draw, [CallerMemberName] string? frameName = null);
}
