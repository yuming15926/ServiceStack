﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack.Text;
using ServiceStack.Text.Json;

namespace ServiceStack.Templates
{
    // ReSharper disable InconsistentNaming
    
    public partial class TemplateDefaultFilters : TemplateDefaultFiltersWithKeywords
    {
        public static TemplateDefaultFilters Instance = new TemplateDefaultFilters();

        // methods without arguments can be used in bindings, e.g. {{ now | dateFormat }}
        public DateTime now() => DateTime.Now;
        public DateTime utcNow() => DateTime.UtcNow;

        public DateTime addTicks(DateTime target, int count) => target.AddTicks(count);
        public DateTime addMilliseconds(DateTime target, int count) => target.AddMilliseconds(count);
        public DateTime addSeconds(DateTime target, int count) => target.AddSeconds(count);
        public DateTime addMinutes(DateTime target, int count) => target.AddMinutes(count);
        public DateTime addHours(DateTime target, int count) => target.AddHours(count);
        public DateTime addDays(DateTime target, int count) => target.AddDays(count);
        public DateTime addMonths(DateTime target, int count) => target.AddMonths(count);
        public DateTime addYears(DateTime target, int count) => target.AddYears(count);

        public List<object> itemsOf(int count, object target)
        {
            AssertWithinMaxQuota(count);
            var to = new List<object>();
            for (var i = 0; i < count; i++)
            {
                to.Add(target);
            }
            return to;
        }

        public object times(int count) => AssertWithinMaxQuota(count).Times().ToList();
        public object range(int count) => Enumerable.Range(0, AssertWithinMaxQuota(count));
        public object range(int start, int count) => Enumerable.Range(start, AssertWithinMaxQuota(count));

        public bool isEven(int value) => value % 2 == 0;
        public bool isOdd(int value) => !isEven(value);

        public static bool isTrue(object target) => target is bool b && b;
        public static bool isFalsy(object target)
        {
            if (target == null || target == JsNull.Value)
                return true;
            if (target is string s)
                return string.IsNullOrEmpty(s);
            if (target is bool b)
                return !b;
            if (target is int i)
                return i == 0;
            if (target is long l)
                return l == 0;
            if (target is double d)
                return d == 0 || double.IsNaN(d);

            return false;
        }

        [HandleUnknownValue] public object iif(object test, object ifTrue, object ifFalse) => isTrue(test) ? ifTrue : ifFalse;
        [HandleUnknownValue] public object when(object returnTarget, object test) => @if(returnTarget, test);     //alias

        [HandleUnknownValue] public object ifNot(object returnTarget, object test) => !isTrue(test) ? returnTarget : null;
        [HandleUnknownValue] public object unless(object returnTarget, object test) => ifNot(returnTarget, test); //alias

        [HandleUnknownValue] public object otherwise(object returnTaget, object elseReturn) => returnTaget ?? elseReturn;

        [HandleUnknownValue] public object ifFalsy(object returnTarget, object test) => isFalsy(test) ? returnTarget : null;
        [HandleUnknownValue] public object ifTruthy(object returnTarget, object test) => !isFalsy(test) ? returnTarget : null;
        [HandleUnknownValue] public object falsy(object test, object returnIfFalsy) => isFalsy(test) ? returnIfFalsy : null;
        [HandleUnknownValue] public object truthy(object test, object returnIfTruthy) => !isFalsy(test) ? returnIfTruthy : null;

        [HandleUnknownValue] public bool isNull(object test) => test == null || test == JsNull.Value;
        [HandleUnknownValue] public bool isNotNull(object test) => !isNull(test);
        [HandleUnknownValue] public bool exists(object test) => !isNull(test);

        [HandleUnknownValue] public bool isZero(double value) => value.Equals(0d);
        [HandleUnknownValue] public bool isPositive(double value) => value > 0;
        [HandleUnknownValue] public bool isNegative(double value) => value < 0;
        [HandleUnknownValue] public bool isNaN(double value) => double.IsNaN(value);
        [HandleUnknownValue] public bool isInfinity(double value) => double.IsInfinity(value);

        [HandleUnknownValue] public object ifExists(object target) => target;
        [HandleUnknownValue] public object ifExists(object returnTarget, object test) => test != null ? returnTarget : null;
        [HandleUnknownValue] public object ifNotExists(object returnTarget, object target) => target == null ? returnTarget : null;
        [HandleUnknownValue] public object ifNo(object returnTarget, object target) => target == null ? returnTarget : null;
        [HandleUnknownValue] public object ifNotEmpty(object target) => isEmpty(target) ? null : target;
        [HandleUnknownValue] public object ifNotEmpty(object returnTarget, object test) => isEmpty(test) ? null : returnTarget;
        [HandleUnknownValue] public object ifEmpty(object returnTarget, object test) => isEmpty(test) ? returnTarget : null;
        [HandleUnknownValue] public object ifTrue(object returnTarget, object test) => isTrue(test) ? returnTarget : null;
        [HandleUnknownValue] public object ifFalse(object returnTarget, object test) => !isTrue(test) ? returnTarget : null;

        [HandleUnknownValue]
        public bool isEmpty(object target)
        {
            if (isNull(target))
                return true;

            if (target is string s)
                return s == string.Empty;

            if (target is IEnumerable e)
                return !e.GetEnumerator().MoveNext();

            return false;
        }

        [HandleUnknownValue] public object end() => StopExecution.Value;
        [HandleUnknownValue] public Task end(TemplateScopeContext scope, object ignore) => TypeConstants.EmptyTask;
        [HandleUnknownValue] public object end(object ignore) => StopExecution.Value;

        [HandleUnknownValue] public object endIfNull(object target) => isNull(target) ? StopExecution.Value : target;
        [HandleUnknownValue] public object endIfNull(object ignoreTarget, object target) => isNull(target) ? StopExecution.Value : target;
        [HandleUnknownValue] public object endIfNotNull(object target) => !isNull(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfNotNull(object ignoreTarget, object target) => !isNull(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfExists(object target) => !isNull(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfExists(object ignoreTarget, object target) => !isNull(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfEmpty(object target) => isEmpty(target) ? StopExecution.Value : target;
        [HandleUnknownValue] public object endIfEmpty(object ignoreTarget, object target) => isEmpty(target) ? StopExecution.Value : target;
        [HandleUnknownValue] public object endIfNotEmpty(object target) => !isEmpty(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfNotEmpty(object ignoreTarget, object target) => !isEmpty(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfFalsy(object target) => isFalsy(target) ? (object) StopExecution.Value : target;
        [HandleUnknownValue] public object endIfFalsy(object ignoreTarget, object target) => isFalsy(target) ? (object) StopExecution.Value : target;
        [HandleUnknownValue] public object endIfTruthy(object target) => !isFalsy(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIfTruthy(object ignoreTarget, object target) => !isFalsy(target) ? (object) StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIf(object test) => isTrue(test) ? (object)StopExecution.Value : IgnoreResult.Value;

        [HandleUnknownValue] public object ifEnd(bool test) => test ? (object)StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object ifEnd(object ignoreTarget, bool test) => test ? (object)StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object ifNotEnd(bool test) => !test ? (object)StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object ifNotEnd(object ignoreTarget, bool test) => !test ? (object)StopExecution.Value : IgnoreResult.Value;
        [HandleUnknownValue] public object endIf(object returnTarget, bool test) => test ? StopExecution.Value : returnTarget;
        [HandleUnknownValue] public object endIfAny(TemplateScopeContext scope, object target, object expression) => any(scope, target, expression) ? StopExecution.Value : target;
        [HandleUnknownValue] public object endIfAll(TemplateScopeContext scope, object target, object expression) => all(scope, target, expression) ? StopExecution.Value : target;
        [HandleUnknownValue] public object endWhere(TemplateScopeContext scope, object target, object expression) => endWhere(scope, target, expression, null);

        [HandleUnknownValue]
        public object endWhere(TemplateScopeContext scope, object target, object expression, object scopeOptions)
        {
            var literal = scope.AssertExpression(nameof(count), expression);
            var scopedParams = scope.GetParamsWithItemBinding(nameof(count), scopeOptions, out string itemBinding);

            literal.ParseConditionExpression(out ConditionExpression expr);
            scope.AddItemToScope(itemBinding, target);
            var result = expr.Evaluate(scope);

            return result
                ? StopExecution.Value
                : target;
        }

        [HandleUnknownValue] public object ifDo(object test) => isTrue(test) ? (object)IgnoreResult.Value : StopExecution.Value;
        [HandleUnknownValue] public object ifDo(object ignoreTarget, object test) => isTrue(test) ? (object)IgnoreResult.Value : StopExecution.Value;
        [HandleUnknownValue] public object doIf(object test) => isTrue(test) ? (object)IgnoreResult.Value : StopExecution.Value;
        [HandleUnknownValue] public object doIf(object ignoreTarget, object test) => isTrue(test) ? (object)IgnoreResult.Value : StopExecution.Value;

        [HandleUnknownValue] public object ifUse(object test, object useValue) => isTrue(test) ? useValue : StopExecution.Value;
        [HandleUnknownValue] public object useIf(object useValue, object test) => isTrue(test) ? useValue : StopExecution.Value;

        public object use(object ignoreTarget, object useValue) => useValue;
        public object show(object ignoreTarget, object useValue) => useValue;
        public object useFmt(object ignoreTarget, string format, object arg) => fmt(format, arg);
        public object useFmt(object ignoreTarget, string format, object arg1, object arg2) => fmt(format, arg1, arg2);
        public object useFmt(object ignoreTarget, string format, object arg1, object arg2, object arg3) => fmt(format, arg1, arg2, arg3);
        public object useFormat(object ignoreTarget, object arg, string fmt) => format(arg, fmt);

        [HandleUnknownValue] public bool isString(object target) => target is string;
        [HandleUnknownValue] public bool isInt(object target) => target is int;
        [HandleUnknownValue] public bool isLong(object target) => target is long;
        [HandleUnknownValue] public bool isInteger(object target) => target?.GetType()?.IsIntegerType() == true;
        [HandleUnknownValue] public bool isDouble(object target) => target is double;
        [HandleUnknownValue] public bool isFloat(object target) => target is float;
        [HandleUnknownValue] public bool isDecimal(object target) => target is decimal;
        [HandleUnknownValue] public bool isBool(object target) => target is bool;
        [HandleUnknownValue] public bool isList(object target) => target is IEnumerable && !(target is IDictionary) && !(target is string);
        [HandleUnknownValue] public bool isEnumerable(object target) => target is IEnumerable;
        [HandleUnknownValue] public bool isDictionary(object target) => target is IDictionary;
        [HandleUnknownValue] public bool isChar(object target) => target is char;
        [HandleUnknownValue] public bool isChars(object target) => target is char[];
        [HandleUnknownValue] public bool isByte(object target) => target is byte;
        [HandleUnknownValue] public bool isBytes(object target) => target is byte[];
        [HandleUnknownValue] public bool isObjectDictionary(object target) => target is IDictionary<string, object>;
        [HandleUnknownValue] public bool isStringDictionary(object target) => target is IDictionary<string, string>;

        [HandleUnknownValue] public bool isType(object target, string typeName) => typeName.EqualsIgnoreCase(target?.GetType()?.Name);
        [HandleUnknownValue] public bool isNumber(object target) => target?.GetType()?.IsNumericType() == true;
        [HandleUnknownValue] public bool isRealNumber(object target) => target?.GetType()?.IsRealNumberType() == true;
        [HandleUnknownValue] public bool isEnum(object target) => target?.GetType()?.IsEnum() == true;
        [HandleUnknownValue] public bool isArray(object target) => target?.GetType()?.IsArray() == true;
        [HandleUnknownValue] public bool isAnonObject(object target) => target?.GetType()?.IsAnonymousType() == true;
        [HandleUnknownValue] public bool isClass(object target) => target?.GetType()?.IsClass() == true;
        [HandleUnknownValue] public bool isValueType(object target) => target?.GetType()?.IsValueType() == true;
        [HandleUnknownValue] public bool isDto(object target) => target?.GetType()?.IsDto() == true;
        [HandleUnknownValue] public bool isTuple(object target) => target?.GetType()?.IsTuple() == true;
        [HandleUnknownValue] public bool isKeyValuePair(object target) => "KeyValuePair`2".Equals(target?.GetType()?.Name);

        [HandleUnknownValue] public int length(object target) => target is IEnumerable e ? e.Cast<object>().Count() : 0;

        [HandleUnknownValue] public bool hasMinCount(object target, int minCount) => target is IEnumerable e && e.Cast<object>().Count() >= minCount;
        [HandleUnknownValue] public bool hasMaxCount(object target, int maxCount) => target is IEnumerable e && e.Cast<object>().Count() <= maxCount;

        public bool or(object lhs, object rhs) => isTrue(lhs) || isTrue(rhs);
        public bool and(object lhs, object rhs) => isTrue(lhs) && isTrue(rhs);

        public bool equals(object target, object other) =>
            target == null || other == null
                ? target == other
                : target.GetType() == other.GetType()
                    ? target.Equals(other)
                    : target.Equals(other.ConvertTo(target.GetType()));

        public bool notEquals(object target, object other) => !equals(target, other);

        public bool greaterThan(object target, object other) => compareTo(target, other, i => i > 0);
        public bool greaterThanEqual(object target, object other) => compareTo(target, other, i => i >= 0);
        public bool lessThan(object target, object other) => compareTo(target, other, i => i < 0);
        public bool lessThanEqual(object target, object other) => compareTo(target, other, i => i <= 0);

        //aliases
        public bool not(bool target) => !target;
        public bool eq(object target, object other) => equals(target, other);
        public bool not(object target, object other) => notEquals(target, other);
        public bool gt(object target, object other) => greaterThan(target, other);
        public bool gte(object target, object other) => greaterThanEqual(target, other);
        public bool lt(object target, object other) => lessThan(target, other);
        public bool lte(object target, object other) => lessThanEqual(target, other);

        internal static bool compareTo(object target, object other, Func<int, bool> fn)
        {
            if (target == null || target == JsNull.Value)
                throw new ArgumentNullException(nameof(target));
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (target is IComparable c)
            {
                return target.GetType() == other?.GetType()
                    ? fn(c.CompareTo(other))
                    : fn(c.CompareTo(other.ConvertTo(target.GetType())));
            }

            throw new NotSupportedException($"{target} is not IComparable");
        }

        public object echo(object value) => value;
        public IRawString pass(string target) => ("{{ " + target + " }}").ToRawString();

        public IEnumerable join(IEnumerable<object> values) => join(values, ",");
        public IEnumerable join(IEnumerable<object> values, string delimiter) => values.Map(x => x.ToString()).Join(delimiter);

        public IEnumerable<object> reverse(TemplateScopeContext scope, IEnumerable<object> original) => original.Reverse();

        public object assign(TemplateScopeContext scope, string argExpr, object value) //from filter
        {
            var targetEndPos = argExpr.IndexOfAny(new[] { '.', '[' });
            if (targetEndPos == -1)
            {
                scope.ScopedParams[argExpr] = value;
            }
            else
            {
                var targetName = argExpr.Substring(0, targetEndPos);
                if (!scope.ScopedParams.TryGetValue(targetName, out object target))
                    throw new NotSupportedException($"Cannot assign to non-existing '{targetName}' in {argExpr}");

                scope.InvokeAssignExpression(argExpr, target, value);
            }

            return value;
        }

        public object assignTo(TemplateScopeContext scope, object value, string argName) //from filter
        {
            scope.ScopedParams[argName] = value;
            return IgnoreResult.Value;
        }

        public Task assignTo(TemplateScopeContext scope, string argName) //from context filter
        {
            var ms = (MemoryStream)scope.OutputStream;
            var value = ms.ReadFully().FromUtf8Bytes();
            scope.ScopedParams[argName] = value;
            ms.SetLength(0); //just capture output, don't write anything to the ResponseStream
            return TypeConstants.EmptyTask;
        }

        public Task partial(TemplateScopeContext scope, object target) => partial(scope, target, null);
        public async Task partial(TemplateScopeContext scope, object target, object scopedParams)
        {
            var pageName = target.ToString();
            var pageParams = scope.AssertOptions(nameof(partial), scopedParams);

            scope.TryGetPage(pageName, out TemplatePage page, out TemplateCodePage codePage);
            if (page != null)
                await page.Init();

            await scope.WritePageAsync(page, codePage, pageParams);
        }

        public Task forEach(TemplateScopeContext scope, object target, object items) => forEach(scope, target, items, null);
        public async Task forEach(TemplateScopeContext scope, object target, object items, object scopeOptions)
        {
            var objs = items as IEnumerable;
            if (objs != null)
            {
                var scopedParams = scope.GetParamsWithItemBinding(nameof(select), scopeOptions, out string itemBinding);

                var i = 0;
                var itemScope = scope.CreateScopedContext(target.ToString(), scopedParams);
                foreach (var item in objs)
                {
                    itemScope.AddItemToScope(itemBinding, item, i++);
                    await itemScope.WritePageAsync();
                }
            }
            else if (items != null)
            {
                throw new ArgumentException($"{nameof(forEach)} in '{scope.Page.VirtualPath}' requires an IEnumerable, but received a '{items.GetType().Name}' instead");
            }
        }

        public string toString(object target) => target?.ToString();
        public List<object> toList(IEnumerable target) => target.Map(x => x);
        public object[] toArray(IEnumerable target) => target.Map(x => x).ToArray();

        public char fromCharCode(int charCode) => Convert.ToChar(charCode);
        public char toChar(object target) => target is string s && s.Length == 1 ? s[0] : target.ConvertTo<char>();
        public char[] toChars(object target) => target is string s
            ? s.ToCharArray()
            : target is IEnumerable<object> objects
                ? objects.Where(x => x != null).Select(x => x.ToString()[0]).ToArray()
                : target.ConvertTo<char[]>();

        public byte[] toUtf8Bytes(string target) => target.ToUtf8Bytes();
        public string fromUtf8Bytes(byte[] target) => target.FromUtf8Bytes();

        public byte toByte(object target) => target.ConvertTo<byte>();
        public int toInt(object target) => target.ConvertTo<int>();
        public long toLong(object target) => target.ConvertTo<long>();
        public float toFloat(object target) => target.ConvertTo<float>();
        public double toDouble(object target) => target.ConvertTo<double>();
        public decimal toDecimal(object target) => target.ConvertTo<decimal>();
        public bool toBool(object target) => target.ConvertTo<bool>();

        public List<string> toKeys(object target)
        {
            if (target == null)
                return null;
            
            if (target is Dictionary<string, object> objDictionary)
                return objDictionary.Keys.ToList();
            if (target is IDictionary dictionary)
                return dictionary.Keys.Map(x => x.ToString());

            if (target is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                var to = new List<string>();
                foreach (var kvp in kvps)
                {
                    to.Add(kvp.Key);
                }
                return to;
            }
            if (target is IEnumerable<KeyValuePair<string, string>> stringKvps)
            {
                var to = new List<string>();
                foreach (var kvp in stringKvps)
                {
                    to.Add(kvp.Key);
                }
                return to;
            }
            throw new NotSupportedException(nameof(toKeys) + " expects an IDictionary or List of KeyValuePairs but received: " + target.GetType().Name);
        }

        public List<object> toValues(object target)
        {
            if (target == null)
                return null;
            
            if (target is Dictionary<string, object> objDictionary)
                return objDictionary.Values.ToList();
            if (target is IDictionary dictionary)
                return dictionary.Values.Map(x => x);

            if (target is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                var to = new List<object>();
                foreach (var kvp in kvps)
                {
                    to.Add(kvp.Value);
                }
                return to;
            }
            if (target is IEnumerable<KeyValuePair<string, string>> stringKvps)
            {
                var to = new List<object>();
                foreach (var kvp in stringKvps)
                {
                    to.Add(kvp.Value);
                }
                return to;
            }
            throw new NotSupportedException(nameof(toValues) + " expects an IDictionary or List of KeyValuePairs but received: " + target.GetType().Name);
        }

        public Dictionary<string, object> toObjectDictionary(object target) => target.ToObjectDictionary();
        public Dictionary<string, string> toStringDictionary(IDictionary map)
        {
            if (isNull(map))
                return null;

            var to = new Dictionary<string, string>();
            foreach (var key in map.Keys)
            {
                var value = map[key];
                to[key.ToString()] = value?.ToString();
            }
            return to;
        }

        public int AssertWithinMaxQuota(int value)
        {
            var maxQuota = (int)Context.Args[TemplateConstants.MaxQuota];
            if (value > maxQuota)
                throw new NotSupportedException($"{value} exceeds Max Quota of {maxQuota}");

            return value;
        }

        public Dictionary<object, object> toDictionary(TemplateScopeContext scope, object target, object expression) => toDictionary(scope, target, expression, null);
        public Dictionary<object, object> toDictionary(TemplateScopeContext scope, object target, object expression, object scopeOptions)
        {
            var items = target.AssertEnumerable(nameof(toDictionary));
            var literal = scope.AssertExpression(nameof(toDictionary), expression);
            var scopedParams = scope.GetParamsWithItemBinding(nameof(toDictionary), scopeOptions, out string itemBinding);

            literal.ToStringSegment().ParseNextToken(out object value, out JsBinding binding);

            return items.ToDictionary(item => scope.AddItemToScope(itemBinding, item).Evaluate(value, binding));
        }

        public IRawString typeName(object target) => (target?.GetType().Name ?? "null").ToRawString();

        public IEnumerable of(TemplateScopeContext scope, IEnumerable target, object scopeOptions)
        {
            var items = target.AssertEnumerable(nameof(of));
            var scopedParams = scope.GetParamsWithItemBinding(nameof(of), scopeOptions, out string itemBinding);

            if (scopedParams.TryGetValue("type", out object oType))
            {
                if (oType is string typeName)
                    return items.Where(x => x?.GetType()?.Name == typeName);
                if (oType is Type type)
                    return items.Where(x => x?.GetType() == type);
            }

            return items;
        }

        public object @do(TemplateScopeContext scope, object expression)
        {
            var literal = scope.AssertExpression(nameof(@do), expression);
            literal.ToStringSegment().ParseNextToken(out object value, out JsBinding binding);
            var result = scope.Evaluate(value, binding);

            return IgnoreResult.Value;
        }

        [HandleUnknownValue]
        public Task @do(TemplateScopeContext scope, object target, object expression) => @do(scope, target, expression, null);
        [HandleUnknownValue]
        public Task @do(TemplateScopeContext scope, object target, object expression, object scopeOptions)
        {
            if (isNull(target) || target is bool b && !b)
                return TypeConstants.EmptyTask;

            var scopedParams = scope.GetParamsWithItemBinding(nameof(@do), scopeOptions, out string itemBinding);
            var literal = scope.AssertExpression(nameof(@do), expression);
            literal.ToStringSegment().ParseNextToken(out object value, out JsBinding binding);

            if (target is IEnumerable objs && !(target is IDictionary) && !(target is string))
            {
                var items = target.AssertEnumerable(nameof(@do));

                var i = 0;
                foreach (var item in items)
                {
                    scope.AddItemToScope(itemBinding, item, i++);
                    var result = scope.Evaluate(value, binding);
                }
            }
            else
            {
                scope.AddItemToScope(itemBinding, target);
                var result = scope.Evaluate(value, binding);
            }

            return TypeConstants.EmptyTask;
        }

        public object property(object target, string propertyName)
        {
            if (isNull(target))
                return null;

            var props = TypeProperties.Get(target.GetType());
            var fn = props.GetPublicGetter(propertyName);
            if (fn == null)
                throw new NotSupportedException($"There is no public Property '{propertyName}' on Type '{target.GetType().Name}'");

            var value = fn(target);
            return value;
        }

        public object field(object target, string fieldName)
        {
            if (isNull(target))
                return null;

            var props = TypeFields.Get(target.GetType());
            var fn = props.GetPublicGetter(fieldName);
            if (fn == null)
                throw new NotSupportedException($"There is no public Field '{fieldName}' on Type '{target.GetType().Name}'");

            var value = fn(target);
            return value;
        }
        
        public object map(TemplateScopeContext scope, object items, object expression) => map(scope, items, expression, null);
        public object map(TemplateScopeContext scope, object target, object expression, object scopeOptions)
        {
            var literal = scope.AssertExpression(nameof(map), expression);
            var scopedParams = scope.GetParamsWithItemBinding(nameof(map), scopeOptions, out string itemBinding);

            literal.ToStringSegment().ParseNextToken(out object value, out JsBinding binding);

            if (target is IEnumerable items && !(target is IDictionary) && !(target is string))
            {
                var i = 0;
                return items.Map(item => scope.AddItemToScope(itemBinding, item, i++).Evaluate(value, binding));
            }

            var result = scope.AddItemToScope(itemBinding, target).Evaluate(value, binding);
            return result;
        }

        public object scopeVars(object target)
        {
            if (isNull(target))
                return null;

            if (target is IDictionary<string, object> g)
                return new ScopeVars(g);

            if (target is IDictionary d)
            {
                var to = new ScopeVars();
                foreach (var key in d.Keys)
                {
                    to[key.ToString()] = d[key];
                }
                return to;
            }

            if (target is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                var to = new ScopeVars();
                foreach (var item in kvps)
                {
                    to[item.Key] = item.Value;
                }
                return to;
            }

            if (target is IEnumerable e)
            {
                var to = new List<object>();

                foreach (var item in e)
                {
                    var toItem = item is IDictionary
                        ? scopeVars(item)
                        : item;

                    to.Add(toItem);
                }

                return to;
            }

            throw new NotSupportedException($"'{nameof(scopeVars)}' expects a Dictionary but received a '{target.GetType().Name}'");
        }

        [HandleUnknownValue]
        public Task select(TemplateScopeContext scope, object target, object selectTemplate) => select(scope, target, selectTemplate, null);
        [HandleUnknownValue]
        public async Task select(TemplateScopeContext scope, object target, object selectTemplate, object scopeOptions)
        {
            if (isNull(target))
                return;

            var scopedParams = scope.GetParamsWithItemBinding(nameof(select), scopeOptions, out string itemBinding);
            var template = JsonTypeSerializer.Unescape(selectTemplate.ToString(), removeQuotes:false);
            var itemScope = scope.CreateScopedContext(template, scopedParams);

            if (target is IEnumerable objs && !(target is IDictionary) && !(target is string))
            {
                var i = 0;
                foreach (var item in objs)
                {
                    itemScope.AddItemToScope(itemBinding, item, i++);
                    await itemScope.WritePageAsync();
                }
            }
            else
            {
                itemScope.AddItemToScope(itemBinding, target);
                await itemScope.WritePageAsync();
            }
        }

        [HandleUnknownValue]
        public Task selectPartial(TemplateScopeContext scope, object target, string pageName) => selectPartial(scope, target, pageName, null);
        [HandleUnknownValue]
        public async Task selectPartial(TemplateScopeContext scope, object target, string pageName, object scopedParams)
        {
            if (isNull(target))
                return;

            scope.TryGetPage(pageName, out TemplatePage page, out TemplateCodePage codePage);
            if (page != null)
                await page.Init();

            var pageParams = scope.GetParamsWithItemBinding(nameof(selectPartial), page, scopedParams, out string itemBinding);

            if (target is IEnumerable objs && !(target is IDictionary) && !(target is string))
            {

                var i = 0;
                foreach (var item in objs)
                {
                    scope.AddItemToScope(itemBinding, item, i++);
                    await scope.WritePageAsync(page, codePage, pageParams);
                }
            }
            else
            {
                scope.AddItemToScope(itemBinding, target);
                await scope.WritePageAsync(page, codePage, pageParams);
            }
        }
        public bool matchesPathInfo(TemplateScopeContext scope, string pathInfo) => 
            scope.GetValue("PathInfo")?.ToString().TrimEnd('/') == pathInfo?.TrimEnd('/');

        public object ifMatchesPathInfo(TemplateScopeContext scope, object returnTarget, string pathInfo) =>
            matchesPathInfo(scope, pathInfo) ? returnTarget : null;
        
        public object withoutNullValues(object target)
        {
            if (target is Dictionary<string, object> objDictionary)
            {
                var keys = objDictionary.Keys.ToList();
                var to = new Dictionary<string, object>();
                foreach (var key in keys)
                {
                    var value = objDictionary[key];
                    if (!isNull(value))
                    {
                        to[key] = value;
                    }
                }
                return to;
            }
            if (target is IEnumerable list)
            {
                var to = new List<object>();
                foreach (var item in list)
                {
                    if (!isNull(item))
                        to.Add(item);
                }
                return to;
            }
            return target;
        }
        
        public object withoutEmptyValues(object target)
        {
            if (target is Dictionary<string, object> objDictionary)
            {
                var keys = objDictionary.Keys.ToList();
                var to = new Dictionary<string, object>();
                foreach (var key in keys)
                {
                    var value = objDictionary[key];
                    if (!isEmpty(value))
                    {
                        to[key] = value;
                    }
                }
                return to;
            }
            if (target is IEnumerable list)
            {
                var to = new List<object>();
                foreach (var item in list)
                {
                    if (!isEmpty(item))
                        to.Add(item);
                }
                return to;
            }
            return target;
        }
   }
}