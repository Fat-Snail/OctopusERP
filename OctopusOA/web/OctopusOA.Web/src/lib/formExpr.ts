/**
 * 轻量表达式求值器（前端运行时）。
 *
 * 语法对齐后端 OctopusOA.Api.Workflow.ExpressionEvaluator：
 *   比较 == != < <= > >=、逻辑 && || !、括号、字段引用、数字/字符串/布尔/null 字面量。
 * 额外支持算术 + - * / %，供「公式字段」使用（公式结果由前端算好写进 formData，
 * 后端只保存 formData，不参与求值，因此不会与后端语法冲突）。
 *
 * 用于：表单字段联动（visibleWhen）、公式字段（formula）、条件分支可视化构建器预览。
 */

export type ExprValue = unknown

type TokenType =
  | 'number' | 'string' | 'bool' | 'null' | 'ident'
  | 'and' | 'or' | 'not'
  | 'eq' | 'ne' | 'le' | 'ge' | 'lt' | 'gt'
  | 'plus' | 'minus' | 'star' | 'slash' | 'percent'
  | 'lparen' | 'rparen' | 'end'

interface Token { type: TokenType; text: string }

function tokenize(input: string): Token[] {
  const tokens: Token[] = []
  let i = 0
  while (i < input.length) {
    const c = input[i]
    if (/\s/.test(c)) { i++; continue }

    // 数字
    if (/[0-9]/.test(c) || (c === '.' && /[0-9]/.test(input[i + 1] ?? ''))) {
      const s = i
      while (i < input.length && /[0-9.]/.test(input[i])) i++
      tokens.push({ type: 'number', text: input.slice(s, i) })
      continue
    }

    // 字符串
    if (c === '"' || c === "'") {
      const quote = c
      i++
      const s = i
      while (i < input.length && input[i] !== quote) i++
      tokens.push({ type: 'string', text: input.slice(s, i) })
      if (i < input.length) i++
      continue
    }

    // 标识符 / 关键字
    if (/[A-Za-z_]/.test(c)) {
      const s = i
      while (i < input.length && /[A-Za-z0-9_]/.test(input[i])) i++
      const word = input.slice(s, i)
      const type: TokenType =
        word === 'true' || word === 'false' ? 'bool' :
        word === 'null' ? 'null' : 'ident'
      tokens.push({ type, text: word })
      continue
    }

    // 双字符运算符
    const two = input.slice(i, i + 2)
    if (two === '&&') { tokens.push({ type: 'and', text: two }); i += 2; continue }
    if (two === '||') { tokens.push({ type: 'or', text: two }); i += 2; continue }
    if (two === '==') { tokens.push({ type: 'eq', text: two }); i += 2; continue }
    if (two === '!=') { tokens.push({ type: 'ne', text: two }); i += 2; continue }
    if (two === '<=') { tokens.push({ type: 'le', text: two }); i += 2; continue }
    if (two === '>=') { tokens.push({ type: 'ge', text: two }); i += 2; continue }

    // 单字符运算符
    const single: Record<string, TokenType> = {
      '!': 'not', '<': 'lt', '>': 'gt',
      '+': 'plus', '-': 'minus', '*': 'star', '/': 'slash', '%': 'percent',
      '(': 'lparen', ')': 'rparen',
    }
    const t = single[c]
    if (!t) throw new Error(`无法识别的字符: '${c}'`)
    tokens.push({ type: t, text: c })
    i++
  }
  tokens.push({ type: 'end', text: '' })
  return tokens
}

function tryNumber(v: ExprValue): number | null {
  if (v == null) return null
  if (typeof v === 'number') return Number.isFinite(v) ? v : null
  if (typeof v === 'boolean') return v ? 1 : 0
  if (typeof v === 'string') {
    if (v.trim() === '') return null
    const n = Number(v)
    return Number.isFinite(n) ? n : null
  }
  return null
}

function truthy(v: ExprValue): boolean {
  if (v == null) return false
  if (typeof v === 'boolean') return v
  if (typeof v === 'string') return v.length > 0
  const n = tryNumber(v)
  return n !== null && n !== 0
}

function compare(op: TokenType, l: ExprValue, r: ExprValue): boolean {
  if (l == null || r == null) {
    if (op === 'eq') return l == null && r == null
    if (op === 'ne') return !(l == null && r == null)
    return false
  }
  const ln = tryNumber(l)
  const rn = tryNumber(r)
  if (ln !== null && rn !== null) {
    switch (op) {
      case 'eq': return ln === rn
      case 'ne': return ln !== rn
      case 'le': return ln <= rn
      case 'ge': return ln >= rn
      case 'lt': return ln < rn
      case 'gt': return ln > rn
    }
  }
  const ls = String(l)
  const rs = String(r)
  switch (op) {
    case 'eq': return ls === rs
    case 'ne': return ls !== rs
    case 'le': return ls <= rs
    case 'ge': return ls >= rs
    case 'lt': return ls < rs
    case 'gt': return ls > rs
    default: return false
  }
}

class Parser {
  private pos = 0
  constructor(private readonly tokens: Token[], private readonly data: Record<string, ExprValue>) {}

  private get cur(): Token { return this.tokens[this.pos] }
  private get atEnd(): boolean { return this.cur.type === 'end' }

  parse(): ExprValue {
    const v = this.parseOr()
    if (!this.atEnd) throw new Error(`表达式存在多余内容: '${this.cur.text}'`)
    return v
  }

  private parseOr(): ExprValue {
    let v = this.parseAnd()
    while (this.cur.type === 'or') { this.pos++; const r = this.parseAnd(); v = truthy(v) || truthy(r) }
    return v
  }

  private parseAnd(): ExprValue {
    let v = this.parseUnary()
    while (this.cur.type === 'and') { this.pos++; const r = this.parseUnary(); v = truthy(v) && truthy(r) }
    return v
  }

  private parseUnary(): ExprValue {
    if (this.cur.type === 'not') { this.pos++; return !truthy(this.parseUnary()) }
    return this.parseComparison()
  }

  private parseComparison(): ExprValue {
    const left = this.parseAdditive()
    const op = this.cur.type
    if (op === 'eq' || op === 'ne' || op === 'le' || op === 'ge' || op === 'lt' || op === 'gt') {
      this.pos++
      const right = this.parseAdditive()
      return compare(op, left, right)
    }
    return left
  }

  private parseAdditive(): ExprValue {
    let v = this.parseMultiplicative()
    while (this.cur.type === 'plus' || this.cur.type === 'minus') {
      const op = this.cur.type
      this.pos++
      const r = this.parseMultiplicative()
      const a = tryNumber(v)
      const b = tryNumber(r)
      if (a === null || b === null) throw new Error('算术运算要求数字操作数')
      v = op === 'plus' ? a + b : a - b
    }
    return v
  }

  private parseMultiplicative(): ExprValue {
    let v = this.parseOperand()
    while (this.cur.type === 'star' || this.cur.type === 'slash' || this.cur.type === 'percent') {
      const op = this.cur.type
      this.pos++
      const r = this.parseOperand()
      const a = tryNumber(v)
      const b = tryNumber(r)
      if (a === null || b === null) throw new Error('算术运算要求数字操作数')
      if ((op === 'slash' || op === 'percent') && b === 0) throw new Error('除数不能为 0')
      v = op === 'star' ? a * b : op === 'slash' ? a / b : a % b
    }
    return v
  }

  private parseOperand(): ExprValue {
    const t = this.cur
    // 括号作为最底层操作数，使其结果能继续参与外层算术/比较（如 (a+b)*c）
    if (t.type === 'lparen') {
      this.pos++
      const v = this.parseOr()
      this.expect('rparen')
      return v
    }
    switch (t.type) {
      case 'number': this.pos++; return Number(t.text)
      case 'string': this.pos++; return t.text
      case 'bool': this.pos++; return t.text === 'true'
      case 'null': this.pos++; return null
      case 'ident': this.pos++; return Object.prototype.hasOwnProperty.call(this.data, t.text) ? this.data[t.text] : null
      default: throw new Error(`意外的标记: '${t.text || t.type}'`)
    }
  }

  private expect(type: TokenType): void {
    if (this.cur.type !== type) throw new Error(`期望 ${type}`)
    this.pos++
  }
}

/** 求值并返回原始值（公式字段用：可能是数字/字符串/布尔）。空表达式返回 null。 */
export function evaluateValue(expression: string | null | undefined, data: Record<string, ExprValue>): ExprValue {
  if (!expression || !expression.trim()) return null
  const tokens = tokenize(expression)
  return new Parser(tokens, data ?? {}).parse()
}

/** 求值并返回布尔（联动条件、条件分支用）。空表达式视为 true。 */
export function evaluate(expression: string | null | undefined, data: Record<string, ExprValue>): boolean {
  if (!expression || !expression.trim()) return true
  return truthy(evaluateValue(expression, data))
}

/** 校验表达式语法，返回错误信息；合法返回 null。构建器实时校验用。 */
export function validateExpression(expression: string): string | null {
  if (!expression || !expression.trim()) return null
  try {
    const tokens = tokenize(expression)
    new Parser(tokens, {}).parse()
    return null
  } catch (e) {
    return e instanceof Error ? e.message : '表达式无效'
  }
}
