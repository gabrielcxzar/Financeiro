import React from 'react';
import './BrandLoading.css';

export default function BrandLoading({ text = 'Carregando...' }) {
  return (
    <div className="brand-loading" role="status" aria-live="polite" aria-busy="true">
      <div className="brand-loading-ring">
        <img src="/brand-mark.svg" alt="" aria-hidden="true" className="brand-loading-logo" />
      </div>
      <p className="brand-loading-text">{text}</p>
    </div>
  );
}
