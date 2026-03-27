(function () {
  var SOURCE = "ajr-preview";

  function notifyParent(type, message) {
    try {
      if (window.parent && window.parent !== window) {
        window.parent.postMessage({ source: SOURCE, type: type, message: message }, "*");
      }
    } catch (_) {
      // Ignore cross-window messaging failures.
    }
  }

  function escapeHtml(value) {
    return String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function getTextareaValue(id) {
    var node = document.getElementById(id);
    if (!(node instanceof HTMLTextAreaElement)) {
      throw new Error("Missing preview payload node: " + id);
    }
    return node.value || "";
  }

  function renderError(message) {
    var output = document.getElementById("output");
    if (output) {
      output.innerHTML =
        '<div class="alert alert-danger">Error rendering template: ' +
        escapeHtml(message) +
        "</div>";
    }
    notifyParent("render-error", message);
  }

  function parseLiteral(expr) {
    var trimmed = String(expr || "").trim();

    if (
      (trimmed.startsWith('"') && trimmed.endsWith('"')) ||
      (trimmed.startsWith("'") && trimmed.endsWith("'"))
    ) {
      return { matched: true, value: trimmed.slice(1, -1) };
    }
    if (trimmed === "true") return { matched: true, value: true };
    if (trimmed === "false") return { matched: true, value: false };
    if (trimmed === "null") return { matched: true, value: null };
    if (trimmed === "undefined") return { matched: true, value: undefined };
    if (/^-?\d+(\.\d+)?$/.test(trimmed)) return { matched: true, value: Number(trimmed) };

    return { matched: false, value: undefined };
  }

  function resolvePathFrom(base, path) {
    if (!path) return base;
    var segments = String(path)
      .split(".")
      .map(function (segment) {
        return segment.trim();
      })
      .filter(Boolean);

    var value = base;
    for (var i = 0; i < segments.length; i += 1) {
      if (value == null || (typeof value !== "object" && typeof value !== "function")) {
        return undefined;
      }
      value = value[segments[i]];
      if (value === undefined) return undefined;
    }

    return value;
  }

  function resolveValue(expr, context, root) {
    var trimmed = String(expr || "").trim();
    if (!trimmed) return context;
    if (trimmed === "." || trimmed === "this") return context;
    if (trimmed === "data") return root;

    var literal = parseLiteral(trimmed);
    if (literal.matched) return literal.value;

    if (trimmed.startsWith("data.")) {
      return resolvePathFrom(root, trimmed.slice(5));
    }

    var fromContext = resolvePathFrom(context, trimmed);
    if (fromContext !== undefined) return fromContext;

    if (context !== root) {
      return resolvePathFrom(root, trimmed);
    }

    return fromContext;
  }

  function evaluateValueExpression(expr, context, root) {
    var expression = String(expr || "").trim();
    var orIndex = expression.indexOf("||");

    if (orIndex !== -1) {
      var left = expression.slice(0, orIndex).trim();
      var right = expression.slice(orIndex + 2).trim();
      var leftValue = resolveValue(left, context, root);
      return leftValue ? leftValue : resolveValue(right, context, root);
    }

    return resolveValue(expression, context, root);
  }

  function evaluateIfExpression(expr, context, root) {
    var terms = String(expr || "")
      .split("&&")
      .map(function (term) {
        return term.trim();
      })
      .filter(Boolean);

    if (!terms.length) {
      return Boolean(resolveValue(expr, context, root));
    }

    for (var i = 0; i < terms.length; i += 1) {
      if (!resolveValue(terms[i], context, root)) return false;
    }

    return true;
  }

  function parseTemplate(template) {
    var root = { type: "root", children: [] };
    var stack = [root];
    var tokenRegex = /\{\{([\s\S]*?)\}\}/g;
    var lastIndex = 0;
    var match;

    while ((match = tokenRegex.exec(template)) !== null) {
      var textChunk = template.slice(lastIndex, match.index);
      if (textChunk) {
        stack[stack.length - 1].children.push({ type: "text", value: textChunk });
      }

      var rawTag = String(match[1] || "").trim();
      if (!rawTag) {
        lastIndex = tokenRegex.lastIndex;
        continue;
      }

      if (rawTag.charAt(0) === "/") {
        var closing = rawTag.slice(1).trim();
        if (closing !== "if" && closing !== "for") {
          throw new Error("Unsupported closing tag: " + rawTag);
        }
        if (stack.length === 1 || stack[stack.length - 1].type !== closing) {
          throw new Error("Mismatched closing tag: " + rawTag);
        }
        stack.pop();
      } else if (rawTag.startsWith("if ")) {
        var ifNode = { type: "if", expr: rawTag.slice(3).trim(), children: [] };
        if (!ifNode.expr) throw new Error("Missing expression in {{if}} block.");
        stack[stack.length - 1].children.push(ifNode);
        stack.push(ifNode);
      } else if (rawTag === "if") {
        throw new Error("Missing expression in {{if}} block.");
      } else if (rawTag === "for" || rawTag.startsWith("for ")) {
        var forNode = { type: "for", expr: rawTag.slice(3).trim(), children: [] };
        stack[stack.length - 1].children.push(forNode);
        stack.push(forNode);
      } else if (rawTag.charAt(0) === ":") {
        var valueExpr = rawTag.slice(1).trim();
        if (!valueExpr) throw new Error("Missing expression in {{:...}} tag.");
        stack[stack.length - 1].children.push({ type: "value", expr: valueExpr });
      } else {
        throw new Error("Unsupported template tag: " + rawTag);
      }

      lastIndex = tokenRegex.lastIndex;
    }

    var tail = template.slice(lastIndex);
    if (tail) {
      stack[stack.length - 1].children.push({ type: "text", value: tail });
    }

    if (stack.length !== 1) {
      throw new Error("Unclosed template block detected.");
    }

    return root.children;
  }

  function normalizeForTarget(value) {
    if (value == null) return [];
    return Array.isArray(value) ? value : [value];
  }

  function renderNodes(nodes, context, root) {
    var html = "";

    for (var i = 0; i < nodes.length; i += 1) {
      var node = nodes[i];

      if (node.type === "text") {
        html += node.value;
        continue;
      }

      if (node.type === "value") {
        var value = evaluateValueExpression(node.expr, context, root);
        html += value == null ? "" : String(value);
        continue;
      }

      if (node.type === "if") {
        if (evaluateIfExpression(node.expr, context, root)) {
          html += renderNodes(node.children, context, root);
        }
        continue;
      }

      if (node.type === "for") {
        var target = node.expr ? resolveValue(node.expr, context, root) : context;
        var items = normalizeForTarget(target);
        for (var j = 0; j < items.length; j += 1) {
          html += renderNodes(node.children, items[j], root);
        }
      }
    }

    return html;
  }

  try {
    var templateText = getTextareaValue("ajr-template");
    var serializedData = getTextareaValue("ajr-data");
    var output = document.getElementById("output");

    if (!output) {
      throw new Error("Missing preview output node.");
    }

    var data;
    try {
      data = JSON.parse(serializedData);
    } catch (error) {
      var parseMessage =
        error && error.message ? error.message : String(error || "Invalid JSON payload");
      throw new Error("Invalid preview data JSON: " + parseMessage);
    }

    var ast = parseTemplate(templateText);
    output.innerHTML = renderNodes(ast, data, data);
    notifyParent("render-success");
  } catch (error) {
    var message = error && error.message ? error.message : String(error || "Unknown render error");
    renderError(message);
  }
})();
