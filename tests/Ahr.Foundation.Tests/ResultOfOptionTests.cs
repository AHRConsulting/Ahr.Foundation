namespace Ahr.Foundation.Tests;

public sealed class ResultOfOptionTests
{
    private static readonly Error _failure = Error.FromMessage("failed");
    private static readonly Error _missing = Error.FromMessage("missing");

    private static Result<Option<int>> Found => Result<Option<int>>.Success(Option<int>.Some(1));
    private static Result<Option<int>> Missing => Result<Option<int>>.Success(Option<int>.None);
    private static Result<Option<int>> Failed => Result<Option<int>>.Failure(_failure);
    private static Result<Option<int>, string> CustomFound => Result<Option<int>, string>.Success(Option<int>.Some(1));
    private static Result<Option<int>, string> CustomMissing => Result<Option<int>, string>.Success(Option<int>.None);
    private static Result<Option<int>, string> CustomFailed => Result<Option<int>, string>.Failure("failed");

    private static string Some(int value) => $"some:{value}";
    private static string None() => "none";
    private static string Fail(Error error) => $"failure:{error.Message}";
    private static string Fail(string error) => $"failure:{error}";

    // -- Match --

    [Fact]
    public void Match_SelectsActiveBranch()
    {
        Assert.Equal("some:1", Found.Match(Some, None, Fail));
        Assert.Equal("none", Missing.Match(Some, None, Fail));
        Assert.Equal("failure:failed", Failed.Match(Some, None, Fail));
    }

    [Fact]
    public void Match_CustomError_SelectsActiveBranch()
    {
        Assert.Equal("some:1", CustomFound.Match(Some, None, Fail));
        Assert.Equal("none", CustomMissing.Match(Some, None, Fail));
        Assert.Equal("failure:failed", CustomFailed.Match(Some, None, Fail));
    }

    [Fact]
    public void Match_NullFunction_ThrowsWithExactParameter()
    {
        Assert.Equal("onSome", Assert.Throws<ArgumentNullException>(() => Failed.Match(null!, None, Fail)).ParamName);
        Assert.Equal("onNone", Assert.Throws<ArgumentNullException>(() => Failed.Match(Some, null!, Fail)).ParamName);
        Assert.Equal("onFailure", Assert.Throws<ArgumentNullException>(() => Found.Match(Some, None, null!)).ParamName);
    }

    // -- MatchAsync on a result --

    [Fact]
    public async Task MatchAsync_SelectsActiveBranch()
    {
        Assert.Equal("some:1", await Found.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error))));
        Assert.Equal("none", await Missing.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error))));
        Assert.Equal("failure:failed", await Failed.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error))));
    }

    [Fact]
    public async Task MatchAsync_CustomError_SelectsActiveBranch()
    {
        Assert.Equal("some:1", await CustomFound.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error))));
        Assert.Equal("none", await CustomMissing.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error))));
        Assert.Equal("failure:failed", await CustomFailed.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error))));
    }

    // -- MatchAsync on a task --

    [Fact]
    public async Task TaskReceiver_MatchAsync_SynchronousBranches_SelectActiveBranch()
    {
        Assert.Equal("some:1", await Task.FromResult(Found).MatchAsync(Some, None, Fail));
        Assert.Equal("none", await Task.FromResult(CustomMissing).MatchAsync(Some, None, Fail));
        Assert.Equal("failure:failed", await Task.FromResult(CustomFailed).MatchAsync(Some, None, Fail));
    }

    [Fact]
    public async Task TaskReceiver_MatchAsync_AsyncLambdas_SelectActiveBranch()
    {
        var found = await Task.FromResult(CustomFound).MatchAsync(
            async value => { await Task.Yield(); return Some(value); },
            async () => { await Task.Yield(); return None(); },
            async error => { await Task.Yield(); return Fail(error); });
        var failed = await Task.FromResult(Failed).MatchAsync(
            async value => { await Task.Yield(); return Some(value); },
            async () => { await Task.Yield(); return None(); },
            async error => { await Task.Yield(); return Fail(error); });

        Assert.Equal("some:1", found);
        Assert.Equal("failure:failed", failed);
    }

    [Fact]
    public async Task TaskReceiver_MatchAsync_NullTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> task = null!;
        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => task.MatchAsync(Some, None, Fail))).ParamName);
    }

    // -- Match with actions --

#pragma warning disable IDE0200, IDE0007 // The lambdas and explicit types are what these tests check.

    private sealed class Calls
    {
        public List<string> Log { get; } = [];

        public void OnSome(int value) => Log.Add($"some:{value}");
        public void OnNone() => Log.Add("none");
        public void OnFail(Error error) => Log.Add($"failure:{error.Message}");
        public void OnFail(string error) => Log.Add($"failure:{error}");

        public async Task OnSomeAsync(int value)
        {
            await Task.Yield();
            OnSome(value);
        }

        public async Task OnNoneAsync()
        {
            await Task.Yield();
            OnNone();
        }

        public async Task OnFailAsync(Error error)
        {
            await Task.Yield();
            OnFail(error);
        }

        public async Task OnFailAsync(string error)
        {
            await Task.Yield();
            OnFail(error);
        }
    }

    [Fact]
    public void MatchActions_StatementLambdas_Compile()
    {
        var calls = new Calls();

        _ = Missing.Match(
            onSome: value => calls.OnSome(value),
            onNone: () => { },
            onFailure: error => calls.OnFail(error));

        Assert.Empty(calls.Log);
    }

    [Fact]
    public void MatchActions_InvokeOnlyTheActiveBranchOnce()
    {
        var found = new Calls();
        var missing = new Calls();
        var failed = new Calls();

        _ = Found.Match(found.OnSome, found.OnNone, found.OnFail);
        _ = Missing.Match(missing.OnSome, missing.OnNone, missing.OnFail);
        _ = Failed.Match(failed.OnSome, failed.OnNone, failed.OnFail);

        Assert.Equal(["some:1"], found.Log);
        Assert.Equal(["none"], missing.Log);
        Assert.Equal(["failure:failed"], failed.Log);
    }

    [Fact]
    public void MatchActions_CustomError_InvokeOnlyTheActiveBranchOnce()
    {
        var found = new Calls();
        var missing = new Calls();
        var failed = new Calls();

        _ = CustomFound.Match(value => found.OnSome(value), () => found.OnNone(), error => found.OnFail(error));
        _ = CustomMissing.Match(missing.OnSome, missing.OnNone, missing.OnFail);
        _ = CustomFailed.Match(failed.OnSome, failed.OnNone, failed.OnFail);

        Assert.Equal(["some:1"], found.Log);
        Assert.Equal(["none"], missing.Log);
        Assert.Equal(["failure:failed"], failed.Log);
    }

    [Fact]
    public void MatchActions_ReturnOriginalResult()
    {
        Assert.Equal(1, Found.Match(_ => { }, () => { }, _ => { }).Value.Value);
        Assert.True(Missing.Match(_ => { }, () => { }, _ => { }).Value.IsNone);
        Assert.Equal(_failure, Failed.Match(_ => { }, () => { }, _ => { }).Error);
        Assert.Equal("failed", CustomFailed.Match(_ => { }, () => { }, _ => { }).Error);
    }

    [Fact]
    public void Match_ValueReturningLambdas_StillReturnTheValue()
    {
        string text = Found.Match(value => $"some:{value}", () => "none", error => error.Message);
        int length = CustomMissing.Match(value => value, () => 0, error => error.Length);

        Assert.Equal("some:1", text);
        Assert.Equal(0, length);
    }

    [Fact]
    public void MatchActions_ThrowingLambdas_BindToActions()
    {
        Result<Option<int>> unchanged = Found.Match(_ => { }, () => throw new InvalidOperationException("none"), _ => throw new InvalidOperationException("failure"));

        Assert.Equal(1, unchanged.Value.Value);
        Assert.Equal("none", Assert.Throws<InvalidOperationException>(() => Missing.Match(_ => { }, () => throw new InvalidOperationException("none"), _ => { })).Message);
        Assert.Equal("some", Assert.Throws<InvalidOperationException>(() => Found.Match(_ => throw new InvalidOperationException("some"), () => throw new InvalidOperationException("none"), _ => throw new InvalidOperationException("failure"))).Message);
    }

    [Fact]
    public void MatchActions_NullAction_ThrowsWithExactParameter()
    {
        Assert.Equal("onSome", Assert.Throws<ArgumentNullException>(() => Failed.Match(null!, () => { }, _ => { })).ParamName);
        Assert.Equal("onNone", Assert.Throws<ArgumentNullException>(() => Found.Match(_ => { }, null!, _ => { })).ParamName);
        Assert.Equal("onFailure", Assert.Throws<ArgumentNullException>(() => CustomFound.Match(_ => { }, () => { }, null!)).ParamName);
    }

    [Fact]
    public async Task MatchAsyncActions_InvokeOnlyTheActiveBranchOnceAndReturnOriginal()
    {
        var found = new Calls();
        var missing = new Calls();
        var failed = new Calls();

        Result<Option<int>> foundResult = await Found.MatchAsync(found.OnSomeAsync, found.OnNoneAsync, found.OnFailAsync);
        Result<Option<int>, string> missingResult = await CustomMissing.MatchAsync(value => missing.OnSomeAsync(value), () => missing.OnNoneAsync(), error => missing.OnFailAsync(error));
        Result<Option<int>> failedResult = await Failed.MatchAsync(
            async value => await failed.OnSomeAsync(value),
            async () => await failed.OnNoneAsync(),
            async error => await failed.OnFailAsync(error));

        Assert.Equal(["some:1"], found.Log);
        Assert.Equal(["none"], missing.Log);
        Assert.Equal(["failure:failed"], failed.Log);
        Assert.Equal(1, foundResult.Value.Value);
        Assert.True(missingResult.Value.IsNone);
        Assert.Equal(_failure, failedResult.Error);
    }

    [Fact]
    public async Task TaskReceiver_MatchAsyncActions_InvokeOnlyTheActiveBranchOnce()
    {
        var found = new Calls();
        var failed = new Calls();

        Result<Option<int>> foundResult = await Task.FromResult(Found).MatchAsync(found.OnSome, found.OnNone, found.OnFail);
        Result<Option<int>, string> failedResult = await Task.FromResult(CustomFailed).MatchAsync(value => failed.OnSome(value), () => { }, error => failed.OnFail(error));

        Assert.Equal(["some:1"], found.Log);
        Assert.Equal(["failure:failed"], failed.Log);
        Assert.Equal(1, foundResult.Value.Value);
        Assert.Equal("failed", failedResult.Error);
    }

    [Fact]
    public async Task TaskReceiver_MatchAsyncTaskDelegates_PreferTheNonGenericOverload()
    {
        var missing = new Calls();
        var failed = new Calls();

        // These lambdas also fit MatchAsync<Task>(Func<T, Task> ...); the Task-returning overload must win, so each Task is awaited.
        Result<Option<int>> missingResult = await Task.FromResult(Missing).MatchAsync(value => missing.OnSomeAsync(value), () => missing.OnNoneAsync(), error => missing.OnFailAsync(error));
        Result<Option<int>, string> failedResult = await Task.FromResult(CustomFailed).MatchAsync(failed.OnSomeAsync, failed.OnNoneAsync, failed.OnFailAsync);

        Assert.Equal(["none"], missing.Log);
        Assert.Equal(["failure:failed"], failed.Log);
        Assert.True(missingResult.Value.IsNone);
        Assert.Equal("failed", failedResult.Error);
    }

    [Fact]
    public async Task TaskReceiver_MatchAsyncActions_NullTaskOrAction_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> task = null!;
        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => task.MatchAsync(_ => { }, () => { }, _ => { }))).ParamName);
        Assert.Equal("onNone", (await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Found).MatchAsync(_ => Task.CompletedTask, null!, _ => Task.CompletedTask))).ParamName);
    }
    // -- TapSome --

    [Fact]
    public void TapSome_RunsOnlyForFoundValueAndReturnsOriginal()
    {
        var calls = new Calls();

        Assert.Equal(1, Found.TapSome(calls.OnSome).Value.Value);
        Assert.True(Missing.TapSome(calls.OnSome).Value.IsNone);
        Assert.Equal(_failure, Failed.TapSome(calls.OnSome).Error);
        Assert.Equal(1, CustomFound.TapSome(calls.OnSome).Value.Value);
        Assert.True(CustomMissing.TapSome(calls.OnSome).Value.IsNone);
        Assert.Equal("failed", CustomFailed.TapSome(calls.OnSome).Error);
        Assert.Equal(["some:1", "some:1"], calls.Log);
    }

    [Fact]
    public async Task TapSomeAsync_RunsOnlyForFoundValueAndReturnsOriginal()
    {
        var calls = new Calls();

        Assert.Equal(1, (await Found.TapSomeAsync(calls.OnSomeAsync)).Value.Value);
        Assert.True((await Missing.TapSomeAsync(calls.OnSomeAsync)).Value.IsNone);
        Assert.Equal("failed", (await CustomFailed.TapSomeAsync(calls.OnSomeAsync)).Error);
        Assert.Equal(["some:1"], calls.Log);
    }

    [Fact]
    public async Task TaskReceiver_TapSomeAsync_RunsSynchronousAndAsynchronousActions()
    {
        var calls = new Calls();

        Result<Option<int>> synchronous = await Task.FromResult(Found).TapSomeAsync(calls.OnSome);
        Result<Option<int>, string> asynchronous = await Task.FromResult(CustomFound).TapSomeAsync(calls.OnSomeAsync);
        Result<Option<int>> failed = await Task.FromResult(Failed).TapSomeAsync(calls.OnSomeAsync);

        Assert.Equal(["some:1", "some:1"], calls.Log);
        Assert.Equal(1, synchronous.Value.Value);
        Assert.Equal(1, asynchronous.Value.Value);
        Assert.Equal(_failure, failed.Error);
    }

    [Fact]
    public async Task TaskReceiver_TapSomeAsync_TaskReturningLambda_IsAwaited()
    {
        var calls = new Calls();

        // This lambda also fits Action<T>; the Func<T, Task> overload must win, so the action finishes before TapSomeAsync does.
        _ = await Task.FromResult(Found).TapSomeAsync(value => calls.OnSomeAsync(value));

        Assert.Equal(["some:1"], calls.Log);
    }

    [Fact]
    public async Task TapSome_NullActionOrTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> task = null!;
        Assert.Equal("action", Assert.Throws<ArgumentNullException>(() => Failed.TapSome(null!)).ParamName);
        Assert.Equal("action", (await Assert.ThrowsAsync<ArgumentNullException>(() => CustomMissing.TapSomeAsync(null!))).ParamName);
        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => task.TapSomeAsync(_ => { }))).ParamName);
    }

    // -- TapSome with a step that can fail --

    private static Result Save(int value) => value > 0 ? Result.Success() : Result.Failure(_missing);
    private static Result<string> SaveWithId(int value) => value > 100 ? Result<string>.Success($"id:{value}") : Result<string>.Failure(_missing);
    private static Result<string, string> CustomSave(int value) => value > 100 ? Result<string, string>.Success($"id:{value}") : Result<string, string>.Failure("rejected");

    [Fact]
    public void TapSome_StepSucceeds_KeepsOriginalResult()
    {
        Assert.Equal(1, Found.TapSome(value => Save(value)).Value.Value);
        Assert.True(Missing.TapSome(value => Save(value)).Value.IsNone);
        Assert.Equal(_failure, Failed.TapSome(value => Save(value)).Error);
    }

    [Fact]
    public void TapSome_StepFails_StopsWithItsFailure()
    {
        Assert.Equal(_missing, Found.TapSome(value => Save(-value)).Error);
        Assert.Equal(_missing, Found.TapSome(value => SaveWithId(value)).Error);
        Assert.Equal("rejected", CustomFound.TapSome(value => CustomSave(value)).Error);
    }

    [Fact]
    public void TapSome_RunsStepOnlyForFoundValue()
    {
        var calls = 0;
        Result Count(int value)
        {
            calls++;
            return Result.Failure(_missing);
        }

        Assert.True(Missing.TapSome(Count).Value.IsNone);
        Assert.Equal(_failure, Failed.TapSome(Count).Error);
        Assert.Equal("failed", CustomFailed.TapSome(value => CustomSave(value)).Error);
        Assert.Equal(0, calls);
        Assert.Equal(_missing, Found.TapSome(Count).Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TapSome_ResultTry_RestoresOnlyWhenFound()
    {
        var restored = new List<int>();

        Result<Option<int>> found = Found.TapSome(value => Result.Try(() => restored.Add(value)));
        Result<Option<int>> failed = Found.TapSome(value => Result.Try(() => throw new InvalidOperationException("corrupt")));

        Assert.Equal([1], restored);
        Assert.Equal(1, found.Value.Value);
        Assert.True(failed.IsFailure);
    }

    [Fact]
    public async Task TapSomeAsync_StepFails_StopsWithItsFailure()
    {
        Assert.Equal(1, (await Found.TapSomeAsync(value => Task.FromResult(Save(value)))).Value.Value);
        Assert.Equal(_missing, (await Found.TapSomeAsync(value => Task.FromResult(Save(-value)))).Error);
        Assert.Equal(_missing, (await Found.TapSomeAsync(value => Task.FromResult(SaveWithId(value)))).Error);
        Assert.Equal("rejected", (await CustomFound.TapSomeAsync(async value => { await Task.Yield(); return CustomSave(value); })).Error);
        Assert.True((await CustomMissing.TapSomeAsync(value => Task.FromResult(CustomSave(value)))).Value.IsNone);
    }

    [Fact]
    public async Task TaskReceiver_TapSomeAsync_StepFails_StopsWithItsFailure()
    {
        // Each lambda also fits an action overload; the result-returning overload must win, so the failure is kept.
        Assert.Equal(_missing, (await Task.FromResult(Found).TapSomeAsync(value => Save(-value))).Error);
        Assert.Equal(_missing, (await Task.FromResult(Found).TapSomeAsync(value => SaveWithId(value))).Error);
        Assert.Equal(_missing, (await Task.FromResult(Found).TapSomeAsync(value => Task.FromResult(Save(-value)))).Error);
        Assert.Equal(_missing, (await Task.FromResult(Found).TapSomeAsync(value => Task.FromResult(SaveWithId(value)))).Error);
        Assert.Equal("rejected", (await Task.FromResult(CustomFound).TapSomeAsync(value => CustomSave(value))).Error);
        Assert.Equal("rejected", (await Task.FromResult(CustomFound).TapSomeAsync(value => Task.FromResult(CustomSave(value)))).Error);
        Assert.Equal(1, (await Task.FromResult(Found).TapSomeAsync(value => Save(value))).Value.Value);
    }

    [Fact]
    public async Task TapSome_NullStep_ThrowsWithExactParameter()
    {
        Func<int, Result> step = null!;
        Func<int, Task<Result<string, string>>> customStep = null!;
        Assert.Equal("action", Assert.Throws<ArgumentNullException>(() => Failed.TapSome(step)).ParamName);
        Assert.Equal("action", (await Assert.ThrowsAsync<ArgumentNullException>(() => CustomMissing.TapSomeAsync(customStep))).ParamName);
    }

    // -- SomeOr --

    [Fact]
    public void SomeOr_UnwrapsSomeAndUsesFallbackForNone()
    {
        Assert.Equal(1, Found.SomeOr(9).Value);
        Assert.Equal(9, Missing.SomeOr(9).Value);
        Assert.Equal(_failure, Failed.SomeOr(9).Error);
        Assert.Equal(1, CustomFound.SomeOr(9).Value);
        Assert.Equal(9, CustomMissing.SomeOr(9).Value);
        Assert.Equal("failed", CustomFailed.SomeOr(9).Error);
    }

    [Fact]
    public async Task SomeOrAsync_ContinuesPipeline()
    {
        Result<int> standard = await Task.FromResult(Missing).SomeOrAsync(9).MapAsync(value => value + 1);
        Result<int, string> custom = await Task.FromResult(CustomFailed).SomeOrAsync(9).MapAsync(value => value + 1);

        Assert.Equal(10, standard.Value);
        Assert.Equal("failed", custom.Error);
    }

    [Fact]
    public async Task SomeOr_NullFallbackOrTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<string>, string>> task = null!;
        Assert.Equal("fallback", Assert.Throws<ArgumentNullException>(() => Result<Option<string>>.Success(Option<string>.None).SomeOr(null!)).ParamName);
        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => task.SomeOrAsync("fallback"))).ParamName);
    }

    // -- BindNone --

    private static Result<Option<int>> Fallback() => Result<Option<int>>.Success(Option<int>.Some(9));
    private static Result<Option<int>, string> CustomFallback() => Result<Option<int>, string>.Success(Option<int>.Some(9));

    [Fact]
    public void BindNone_TriesFallbackOnlyForNone()
    {
        Assert.Equal(1, Found.BindNone(Fallback).Value.Value);
        Assert.Equal(9, Missing.BindNone(Fallback).Value.Value);
        Assert.Equal(_failure, Failed.BindNone(Fallback).Error);
        Assert.Equal(1, CustomFound.BindNone(CustomFallback).Value.Value);
        Assert.Equal(9, CustomMissing.BindNone(CustomFallback).Value.Value);
        Assert.Equal("failed", CustomFailed.BindNone(CustomFallback).Error);
    }

    [Fact]
    public void BindNone_InvokesFallbackOnlyForNoneAndKeepsItsOutcome()
    {
        var calls = 0;
        Result<Option<int>> Count()
        {
            calls++;
            return Result<Option<int>>.Failure(_missing);
        }

        _ = Found.BindNone(Count);
        _ = Failed.BindNone(Count);
        Assert.Equal(0, calls);
        Assert.Equal(_missing, Missing.BindNone(Count).Error);
        Assert.Equal(1, calls);
        Assert.True(Missing.BindNone(() => Missing).Value.IsNone);
    }

    [Fact]
    public async Task BindNoneAsync_TriesFallbackOnlyForNone()
    {
        Assert.Equal(1, (await Found.BindNoneAsync(() => Task.FromResult(Fallback()))).Value.Value);
        Assert.Equal(9, (await Missing.BindNoneAsync(() => Task.FromResult(Fallback()))).Value.Value);
        Assert.Equal("failed", (await CustomFailed.BindNoneAsync(() => Task.FromResult(CustomFallback()))).Error);
        Assert.Equal(9, (await Task.FromResult(Missing).BindNoneAsync(Fallback)).Value.Value);
        Assert.Equal(9, (await Task.FromResult(CustomMissing).BindNoneAsync(async () => { await Task.Yield(); return CustomFallback(); })).Value.Value);
        Assert.Equal(1, (await Task.FromResult(CustomFound).BindNoneAsync(() => Task.FromResult(CustomFallback()))).Value.Value);
    }

    [Fact]
    public async Task BindNone_NullFunctionOrTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> task = null!;
        Assert.Equal("bind", Assert.Throws<ArgumentNullException>(() => Found.BindNone(null!)).ParamName);
        Assert.Equal("bind", (await Assert.ThrowsAsync<ArgumentNullException>(() => CustomFound.BindNoneAsync(null!))).ParamName);
        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => task.BindNoneAsync(Fallback))).ParamName);
    }

#pragma warning restore IDE0200, IDE0007

    // -- EnsureSome --

    [Fact]
    public void EnsureSome_UnwrapsSomeAndFailsNone()
    {
        Assert.Equal(1, Found.EnsureSome(_missing).Value);
        Assert.Equal(_missing, Missing.EnsureSome(_missing).Error);
    }

    [Fact]
    public void EnsureSome_AlreadyFailed_KeepsOriginalError() => Assert.Equal(_failure, Failed.EnsureSome(_missing).Error);

    [Fact]
    public void EnsureSome_CustomError_UnwrapsSomeAndFailsNone()
    {
        Assert.Equal(1, CustomFound.EnsureSome("missing").Value);
        Assert.Equal("missing", CustomMissing.EnsureSome("missing").Error);
        Assert.Equal("failed", CustomFailed.EnsureSome("missing").Error);
    }

    [Fact]
    public void EnsureSome_InvalidError_ThrowsEvenWhenFailed()
    {
        Assert.Equal("error", Assert.Throws<ArgumentNullException>(() => Failed.EnsureSome(default)).ParamName);
        Assert.Equal("error", Assert.Throws<ArgumentNullException>(() => CustomFailed.EnsureSome(null!)).ParamName);
    }

    [Fact]
    public async Task EnsureSomeAsync_ContinuesPipeline()
    {
        Result<int> standard = await Task.FromResult(Found).EnsureSomeAsync(_missing).MapAsync(value => value + 1);
        Result<int, string> custom = await Task.FromResult(CustomMissing).EnsureSomeAsync("missing").MapAsync(value => value + 1);

        Assert.Equal(2, standard.Value);
        Assert.Equal("missing", custom.Error);
    }

    // -- MapSome --

    [Fact]
    public void MapSome_MapsFoundValueAndPassesOthersThrough()
    {
        Assert.Equal(2, Found.MapSome(value => value + 1).Value.Value);
        Assert.True(Missing.MapSome(value => value + 1).Value.IsNone);
        Assert.Equal(_failure, Failed.MapSome(value => value + 1).Error);
        Assert.Equal(2, CustomFound.MapSome(value => value + 1).Value.Value);
        Assert.True(CustomMissing.MapSome(value => value + 1).Value.IsNone);
        Assert.Equal("failed", CustomFailed.MapSome(value => value + 1).Error);
    }

    [Fact]
    public void MapSome_InvokesMappingOnlyForFoundValue()
    {
        var calls = 0;
        int Count(int value)
        {
            calls++;
            return value;
        }

        _ = Missing.MapSome(Count);
        _ = Failed.MapSome(Count);
        Assert.Equal(0, calls);
        _ = Found.MapSome(Count);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void MapSome_NullMappingResult_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Found.MapSome<int, string>(_ => null!));

    [Fact]
    public async Task MapSomeAsync_MapsFoundValueAndPassesOthersThrough()
    {
        Assert.Equal(2, (await Found.MapSomeAsync(value => Task.FromResult(value + 1))).Value.Value);
        Assert.True((await CustomMissing.MapSomeAsync(value => Task.FromResult(value + 1))).Value.IsNone);
        Assert.Equal("failed", (await CustomFailed.MapSomeAsync(value => Task.FromResult(value + 1))).Error);
        Assert.Equal(2, (await Task.FromResult(Found).MapSomeAsync(value => value + 1)).Value.Value);
        Assert.Equal(2, (await Task.FromResult(CustomFound).MapSomeAsync(async value => { await Task.Yield(); return value + 1; })).Value.Value);
    }

    // -- BindSome --

    private static Result<Option<string>> Lookup(int value) => Result<Option<string>>.Success(Option<string>.Some($"order:{value}"));
    private static Result<Option<string>, string> CustomLookup(int value) => Result<Option<string>, string>.Success(Option<string>.Some($"order:{value}"));

    [Fact]
    public void BindSome_ContinuesFoundValueAndPassesOthersThrough()
    {
        Assert.Equal("order:1", Found.BindSome(Lookup).Value.Value);
        Assert.True(Missing.BindSome(Lookup).Value.IsNone);
        Assert.Equal(_failure, Failed.BindSome(Lookup).Error);
        Assert.Equal("order:1", CustomFound.BindSome(CustomLookup).Value.Value);
        Assert.True(CustomMissing.BindSome(CustomLookup).Value.IsNone);
        Assert.Equal("failed", CustomFailed.BindSome(CustomLookup).Error);
    }

    [Fact]
    public void BindSome_InnerOutcomeIsKept()
    {
        Assert.True(Found.BindSome(_ => Result<Option<string>>.Success(Option<string>.None)).Value.IsNone);
        Assert.Equal(_missing, Found.BindSome(_ => Result<Option<string>>.Failure(_missing)).Error);
    }

    [Fact]
    public async Task BindSomeAsync_ContinuesFoundValueAndPassesOthersThrough()
    {
        Assert.Equal("order:1", (await Found.BindSomeAsync(value => Task.FromResult(Lookup(value)))).Value.Value);
        Assert.True((await Missing.BindSomeAsync(value => Task.FromResult(Lookup(value)))).Value.IsNone);
        Assert.Equal("failed", (await CustomFailed.BindSomeAsync(value => Task.FromResult(CustomLookup(value)))).Error);
        Assert.Equal("order:1", (await Task.FromResult(Found).BindSomeAsync(Lookup)).Value.Value);
        Assert.True((await Task.FromResult(CustomMissing).BindSomeAsync(value => Task.FromResult(CustomLookup(value)))).Value.IsNone);
    }

    [Fact]
    public void MapSomeAndBindSome_NullFunction_ThrowsWithExactParameter()
    {
        Assert.Equal("map", Assert.Throws<ArgumentNullException>(() => Failed.MapSome<int, int>(null!)).ParamName);
        Assert.Equal("bind", Assert.Throws<ArgumentNullException>(() => CustomFailed.BindSome<int, string, int>(null!)).ParamName);
    }

    [Fact]
    public async Task Chain_BindSomeMapSomeMatch_ReadsAsOnePipeline()
    {
        var text = await Task.FromResult(CustomFound)
            .BindSomeAsync(value => Task.FromResult(CustomLookup(value)))
            .MapSomeAsync(order => order.Length)
            .MatchAsync(length => $"{length}", () => "none", error => error);

        Assert.Equal("7", text);
    }

    // -- Flattening nested results with Bind --

    [Fact]
    public void Bind_Identity_FlattensNestedResults()
    {
        Assert.True(Result<Result>.Success(Result.Success()).Bind(inner => inner).IsSuccess);
        Assert.Equal(_failure, Result<Result>.Success(Result.Failure(_failure)).Bind(inner => inner).Error);
        Assert.Equal(1, Result<Result<int>>.Success(Result<int>.Success(1)).Bind(inner => inner).Value);
        Assert.Equal(1, Result<Result<int, string>, string>.Success(Result<int, string>.Success(1)).Bind(inner => inner).Value);
        Assert.Equal("failed", Result<Result<int, string>, string>.Failure("failed").Bind(inner => inner).Error);
    }
}
