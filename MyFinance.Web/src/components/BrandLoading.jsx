import React from 'react';
import './BrandLoading.css';

export default function BrandLoading({ text = 'Carregando painel financeiro...' }) {
  return (
    <div className="brand-loading" role="status" aria-live="polite" aria-busy="true">
      <div className="brand-loading-stage">
        <div className="brand-loading-spinner" />
        <div className="brand-loading-logo-box">
          <img src="/brand-mark.svg" alt="Finflow" className="brand-loading-logo" />
        </div>
      </div>
      <p className="brand-loading-text">{text}</p>
    </div>
  );
}

