import React from 'react';
import styled from 'styled-components';
import {
  HomeOutlined,
  UnorderedListOutlined,
  PlusCircleOutlined,
  BankOutlined,
  MenuOutlined,
} from '@ant-design/icons';

const NavContainer = styled.nav`
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 64px;
  background: #0B0D12;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  display: flex;
  justify-content: space-around;
  align-items: center;
  z-index: 998;
  padding: 0 8px;
  box-shadow: 0 -8px 24px rgba(0, 0, 0, 0.3);

  @media (min-width: 993px) {
    display: none;
  }
`;

const NavItem = styled.button`
  background: transparent;
  border: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  color: ${(props) => (props.$active ? '#FF6600' : '#8E9BAE')};
  font-size: 11px;
  font-weight: ${(props) => (props.$active ? '700' : '500')};
  cursor: pointer;
  padding: 6px 10px;
  border-radius: 8px;
  transition: color 0.15s ease;

  .icon {
    font-size: 20px;
  }

  &:hover {
    color: #FF6600;
  }
`;

export default function BottomNavigation({ activeKey, onSelect, onOpenMenu, onAddTransaction }) {
  return (
    <NavContainer>
      <NavItem $active={activeKey === '1'} onClick={() => onSelect('1')}>
        <HomeOutlined className="icon" />
        <span>Início</span>
      </NavItem>

      <NavItem $active={activeKey === '2'} onClick={() => onSelect('2')}>
        <UnorderedListOutlined className="icon" />
        <span>Extrato</span>
      </NavItem>

      <NavItem $active={false} onClick={onAddTransaction} style={{ color: '#FF6600' }}>
        <PlusCircleOutlined className="icon" style={{ fontSize: 24 }} />
        <span>Novo</span>
      </NavItem>

      <NavItem $active={activeKey === '4'} onClick={() => onSelect('4')}>
        <BankOutlined className="icon" />
        <span>Contas</span>
      </NavItem>

      <NavItem $active={false} onClick={onOpenMenu}>
        <MenuOutlined className="icon" />
        <span>Menu</span>
      </NavItem>
    </NavContainer>
  );
}
