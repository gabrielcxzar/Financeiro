import React, { useMemo } from 'react';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
} from 'chart.js';
import { Bar } from 'react-chartjs-2';

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend);

export const options = {
  responsive: true,
  maintainAspectRatio: false, // Permite ajustar altura livremente
  plugins: {
    legend: { position: 'bottom' },
    title: { display: false },
  },
  scales: {
    y: { beginAtZero: true },
    x: { grid: { display: false } }
  }
};

export default function DashboardCharts({ transactions }) {
  // Lógica Inteligente: Agrupa transações por categoria
  const chartData = useMemo(() => {
    const categoryTotals = {};

    transactions.forEach(t => {
      // Só queremos somar DESPESAS no gráfico
      if (t.type === 'Expense') {
        const catName = t.category?.name || 'Outros';
        // Soma acumulada
        categoryTotals[catName] = (categoryTotals[catName] || 0) + t.amount;
      }
    });

    return {
      labels: Object.keys(categoryTotals),
      datasets: [
        {
          label: 'Despesas (R$)',
          data: Object.values(categoryTotals),
          backgroundColor: 'rgba(24, 144, 255, 0.6)', // Azul bonito
          borderColor: 'rgba(24, 144, 255, 1)',
          borderWidth: 1,
          borderRadius: 4,
        },
      ],
    };
  }, [transactions]); // Recalcula sempre que as transações mudarem

  return (
    <div style={{ height: 300 }}>
      <Bar options={options} data={chartData} />
    </div>
  );
}