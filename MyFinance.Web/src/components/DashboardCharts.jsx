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

export default function DashboardCharts({ transactions, compact = false }) {
  const chartData = useMemo(() => {
    const categoryTotals = {};

    transactions.forEach((t) => {
      if (t.type === 'Expense') {
        const catName = t.category?.name || 'Outros';
        categoryTotals[catName] = (categoryTotals[catName] || 0) + t.amount;
      }
    });

    return {
      labels: Object.keys(categoryTotals),
      datasets: [
        {
          label: 'Despesas (R$)',
          data: Object.values(categoryTotals),
          backgroundColor: 'rgba(24, 144, 255, 0.6)',
          borderColor: 'rgba(24, 144, 255, 1)',
          borderWidth: 1,
          borderRadius: 4,
        },
      ],
    };
  }, [transactions]);

  const options = useMemo(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: compact ? 'top' : 'bottom' },
        title: { display: false },
      },
      scales: {
        y: { beginAtZero: true },
        x: {
          grid: { display: false },
          ticks: {
            autoSkip: true,
            maxRotation: compact ? 35 : 0,
            minRotation: compact ? 25 : 0,
          },
        },
      },
    }),
    [compact],
  );

  return (
    <div style={{ height: compact ? 260 : 300 }}>
      <Bar options={options} data={chartData} />
    </div>
  );
}
