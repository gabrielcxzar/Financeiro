import React, { useState } from 'react';
import { Input, Tooltip } from 'antd';
import { CalculatorOutlined } from '@ant-design/icons';

// Safely evaluate simple math expressions like "150 / 3" or "45.50 + 12"
function evaluateMathExpression(expr) {
  if (!expr || typeof expr !== 'string') return null;
  
  // Clean string: allow numbers, operators +, -, *, /, parentheses, commas and dots
  const sanitized = expr.replace(/R\$\s?/g, '').replace(/\,/g, '.').trim();

  // Check if it's a valid expression with only safe math characters
  if (!/^[0-9+\-*/.()\s]+$/.test(sanitized)) {
    return null;
  }

  try {
    // Function constructor safer evaluation for basic arithmetic
    // eslint-disable-next-line no-new-func
    const result = new Function(`return (${sanitized});`)();
    if (typeof result === 'number' && !isNaN(result) && isFinite(result)) {
      return Math.round(result * 100) / 100;
    }
  } catch {
    return null;
  }
  return null;
}

export default function InputMoney({ value, onChange, placeholder = '0,00', ...props }) {
  const [displayValue, setDisplayValue] = useState(value !== undefined && value !== null ? String(value) : '');

  React.useEffect(() => {
    if (value !== undefined && value !== null && !isNaN(value)) {
      setDisplayValue(String(value));
    }
  }, [value]);

  const handleBlur = () => {
    if (!displayValue) {
      if (onChange) onChange(0);
      return;
    }

    const calculated = evaluateMathExpression(displayValue);
    if (calculated !== null) {
      setDisplayValue(String(calculated));
      if (onChange) onChange(calculated);
    } else {
      const parsed = parseFloat(displayValue.replace(/\,/g, '.'));
      if (!isNaN(parsed)) {
        if (onChange) onChange(parsed);
      }
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      handleBlur();
    }
  };

  const handleChange = (e) => {
    const val = e.target.value;
    setDisplayValue(val);
  };

  return (
    <Tooltip title="Dica: Você pode digitar expressões matemáticas! Ex: 150 / 3 ou 45 + 12.50" placement="topRight">
      <Input
        {...props}
        prefix={<span style={{ color: '#FF6600', fontWeight: 700, marginRight: 4 }}>R$</span>}
        suffix={<CalculatorOutlined style={{ color: '#94A3B8' }} />}
        value={displayValue}
        onChange={handleChange}
        onBlur={handleBlur}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        style={{ borderRadius: 10, ...props.style }}
      />
    </Tooltip>
  );
}
