import React from 'react';
import { InputNumber } from 'antd';

export default function InputMoney(props) {
  return (
    <InputNumber
      {...props}
      style={{ width: '100%', ...props.style }}
      prefix="R$"
      decimalSeparator=","
      // Garante separador de milhar e decimal no padrao brasileiro
      formatter={(value) => 
        `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
      }
      parser={(value) => value?.replace(/R\$\s?|\./g, '').replace(',', '.')}
      precision={2}
    />
  );
}
