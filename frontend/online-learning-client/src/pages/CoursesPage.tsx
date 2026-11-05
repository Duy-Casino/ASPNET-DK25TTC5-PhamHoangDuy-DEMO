import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { SlidersHorizontal, X } from 'lucide-react'
import { CourseGrid } from '../components/course/CourseCard'
import { EmptyState, ErrorState, Pagination } from '../components/ui'
import { useAsync, useDocumentTitle } from '../hooks/useAsync'
import { useReveal } from '../hooks/useReveal'
import { catalogService, type CourseSearchParams } from '../services/api'
import { courseLevelLabel, type CourseLevel } from '../types/common'
import { formatNumber } from '../utils/format'

const sorts: { value: NonNullable<CourseSearchParams['sort']>; label: string }[] = [
  { value: 'newest', label: 'Mới nhất' },
  { value: 'popular', label: 'Phổ biến' },
  { value: 'rating', label: 'Đánh giá cao' },
  { value: 'price_asc', label: 'Giá thấp → cao' },
  { value: 'price_desc', label: 'Giá cao → thấp' },
]

const priceRanges = [
  { key: '', label: 'Mọi mức giá' },
  { key: 'free', label: 'Miễn phí' },
  { key: '0-500000', label: 'Dưới 500.000đ' },
  { key: '500000-1000000', label: '500.000đ – 1.000.000đ' },
  { key: '1000000-', label: 'Trên 1.000.000đ' },
]

export default function CoursesPage() {
  const [params, setParams] = useSearchParams()
  const [filtersOpen, setFiltersOpen] = useState(false)
  const ref = useReveal<HTMLDivElement>()

  const keyword = params.get('keyword') ?? ''
  const category = params.get('category') ?? ''
  const level = (params.get('level') ?? '') as CourseLevel | ''
  const sort = (params.get('sort') ?? 'newest') as NonNullable<CourseSearchParams['sort']>
  const minRating = params.get('minRating') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const price = params.get('free') === 'true' ? 'free' : params.get('price') ?? ''

  useDocumentTitle(keyword ? `Tìm "${keyword}"` : 'Tất cả khóa học')

  const categories = useAsync(() => catalogService.categories(), [])

  const query: CourseSearchParams = {
    keyword: keyword || undefined,
    category: category || undefined,
    level: level || undefined,
    sort,
    minRating: minRating ? Number(minRating) : undefined,
    page,
    pageSize: 12,
    free: price === 'free' ? true : undefined,
  }
  if (price && price !== 'free') {
    const [min, max] = price.split('-')
    if (min) query.minPrice = Number(min)
    if (max) query.maxPrice = Number(max)
  }

  const result = useAsync(() => catalogService.search(query), [params.toString()])

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v) next.set(k, v)
      else next.delete(k)
    }
    if (!('page' in changes)) next.delete('page')
    setParams(next)
  }

  const setPrice = (key: string) =>
    update(key === 'free' ? { free: 'true', price: null } : { free: null, price: key || null })

  const activeCategory = categories.data?.flatMap((c) => [c, ...c.children]).find((c) => c.slug === category)
  const hasFilters = !!(category || level || minRating || price || keyword)

  const filters = (
    <div className="flex flex-col gap-8">
      <fieldset>
        <legend className="mb-3 font-bold">Lĩnh vực</legend>
        <div className="flex flex-col gap-1">
          <FilterOption checked={!category} onChange={() => update({ category: null })} name="category">Tất cả</FilterOption>
          {categories.data?.map((c) => (
            <FilterOption key={c.id} checked={category === c.slug} onChange={() => update({ category: c.slug })} name="category">
              {c.name} <span className="text-ink-muted">({c.courseCount})</span>
            </FilterOption>
          ))}
        </div>
      </fieldset>
      <fieldset>
        <legend className="mb-3 font-bold">Cấp độ</legend>
        <div className="flex flex-col gap-1">
          <FilterOption checked={!level} onChange={() => update({ level: null })} name="level">Mọi cấp độ</FilterOption>
          {(Object.keys(courseLevelLabel) as CourseLevel[]).map((l) => (
            <FilterOption key={l} checked={level === l} onChange={() => update({ level: l })} name="level">{courseLevelLabel[l]}</FilterOption>
          ))}
        </div>
      </fieldset>
      <fieldset>
        <legend className="mb-3 font-bold">Giá</legend>
        <div className="flex flex-col gap-1">
          {priceRanges.map((p) => (
            <FilterOption key={p.key} checked={price === p.key} onChange={() => setPrice(p.key)} name="price">{p.label}</FilterOption>
          ))}
        </div>
      </fieldset>
      <fieldset>
        <legend className="mb-3 font-bold">Đánh giá</legend>
        <div className="flex flex-col gap-1">
          <FilterOption checked={!minRating} onChange={() => update({ minRating: null })} name="rating">Tất cả</FilterOption>
          {['4.5', '4', '3.5'].map((r) => (
            <FilterOption key={r} checked={minRating === r} onChange={() => update({ minRating: r })} name="rating">Từ {r} sao</FilterOption>
          ))}
        </div>
      </fieldset>
    </div>
  )

  return (
    <div ref={ref} className="container-page py-12 md:py-16">
      <div className="flex flex-wrap items-end justify-between gap-6">
        <div>
          <h1 className="text-title font-extrabold">
            {keyword ? `Kết quả cho “${keyword}”` : activeCategory ? activeCategory.name : 'Tất cả khóa học'}
          </h1>
          <p className="mt-2 text-lg text-ink-soft" aria-live="polite">
            {result.data ? `${formatNumber(result.data.totalCount)} khóa học` : 'Đang tìm…'}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button type="button" className="btn-soft lg:hidden" onClick={() => setFiltersOpen(true)}>
            <SlidersHorizontal className="size-4" aria-hidden="true" /> Bộ lọc
          </button>
          <label className="flex items-center gap-2">
            <span className="text-sm font-semibold">Sắp xếp</span>
            <select value={sort} onChange={(e) => update({ sort: e.target.value })} className="input min-h-11 w-auto pr-10">
              {sorts.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          </label>
        </div>
      </div>

      {hasFilters && (
        <button type="button" onClick={() => setParams(new URLSearchParams())} className="btn-ghost mt-4 px-3 text-brand-700">
          <X className="size-4" aria-hidden="true" /> Xóa bộ lọc
        </button>
      )}

      <div className="mt-10 grid gap-12 lg:grid-cols-[240px_1fr]">
        <aside className="hidden lg:block" aria-label="Bộ lọc">{filters}</aside>

        {filtersOpen && (
          <div className="fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Bộ lọc">
            <button type="button" className="absolute inset-0 bg-ink/40" aria-label="Đóng bộ lọc" onClick={() => setFiltersOpen(false)} />
            <div className="rise-in absolute inset-x-0 bottom-0 max-h-[85svh] overflow-y-auto rounded-t-3xl bg-white p-6 pb-[max(1.5rem,env(safe-area-inset-bottom))]">
              <div className="mb-6 flex items-center justify-between">
                <h2 className="text-xl font-bold">Bộ lọc</h2>
                <button type="button" className="btn-ghost px-3" onClick={() => setFiltersOpen(false)}>Xong</button>
              </div>
              {filters}
            </div>
          </div>
        )}

        <section aria-label="Danh sách khóa học">
          {result.error ? (
            <ErrorState message={result.error} onRetry={result.reload} />
          ) : !result.loading && result.data?.items.length === 0 ? (
            <EmptyState
              title="Không tìm thấy khóa học phù hợp"
              action={<button type="button" className="btn-primary" onClick={() => setParams(new URLSearchParams())}>Xem tất cả khóa học</button>}
            >
              Thử từ khóa ngắn hơn hoặc bỏ bớt bộ lọc.
            </EmptyState>
          ) : (
            <>
              <CourseGrid courses={result.data?.items ?? []} loading={result.loading} skeletons={6} columns={3} />
              <div className="mt-12">
                <Pagination page={page} totalPages={result.data?.totalPages ?? 1} onChange={(p) => { update({ page: String(p) }); window.scrollTo({ top: 0, behavior: 'smooth' }) }} />
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  )
}

function FilterOption({ checked, onChange, name, children }: { checked: boolean; onChange: () => void; name: string; children: React.ReactNode }) {
  return (
    <label className={`flex min-h-10 cursor-pointer items-center gap-3 rounded-lg px-3 transition-colors ${checked ? 'bg-brand-50 font-semibold text-brand-800' : 'hover:bg-surface-2'}`}>
      <input type="radio" name={name} checked={checked} onChange={onChange} className="size-4 accent-brand-600" />
      <span>{children}</span>
    </label>
  )
}
