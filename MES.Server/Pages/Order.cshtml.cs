namespace MES.Server.Pages
{
    public class OrderModel : PageModel
    {
        private readonly OrderService _orderService;

        [BindProperty]
        public int RequestQty { get; set; } = 1;

        [BindProperty]
        public string ModelCode { get; set; } = "KIA_CARNIVAL";

        public string? Message { get; private set; }

        public OrderModel(OrderService orderService)
        {
            _orderService = orderService;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                if (_orderService.CreateWebOrder(ModelCode, RequestQty, out var errorMessage))
                {
                    Message = $"주문이 접수되었습니다. (모델: {ModelCode}, 수량: {RequestQty})";
                }
                else
                {
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        ModelState.AddModelError(string.Empty, errorMessage);
                    }
                }

                return Page();
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "주문 처리 중 오류가 발생했습니다. 다시 시도해 주세요.");
                return Page();
            }
        }
    }
}
