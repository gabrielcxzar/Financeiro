import React from 'react';
import { Button, Tooltip } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import styled from 'styled-components';

const FABWrapper = styled.div`
  position: fixed;
  bottom: 28px;
  right: 28px;
  z-index: 999;

  @media (max-width: 992px) {
    bottom: 84px; /* Above bottom navigation bar */
    right: 20px;
  }
`;

const FABButton = styled(Button)`
  width: 58px !important;
  height: 58px !important;
  border-radius: 50% !important;
  background: linear-gradient(135deg, #FF6600 0%, #FF8800 100%) !important;
  border: none !important;
  box-shadow: 0 10px 25px rgba(255, 102, 0, 0.45) !important;
  display: flex !important;
  align-items: center !important;
  justify-content: center !important;
  color: #FFFFFF !important;
  font-size: 24px !important;
  transition: transform 0.2s ease, box-shadow 0.2s ease !important;

  &:hover {
    transform: scale(1.08) rotate(90deg) !important;
    box-shadow: 0 14px 30px rgba(255, 102, 0, 0.6) !important;
    color: #FFFFFF !important;
  }

  &:active {
    transform: scale(0.96) !important;
  }
`;

export default function FloatingActionButton({ onClick }) {
  return (
    <FABWrapper>
      <Tooltip title="Nova Transação (+)" placement="left">
        <FABButton type="primary" icon={<PlusOutlined />} onClick={onClick} aria-label="Nova Transação" />
      </Tooltip>
    </FABWrapper>
  );
}
